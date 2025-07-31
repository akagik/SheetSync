using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using KoheiUtils;
using KoheiUtils.Reflections;
using SheetSync.Services;

namespace SheetSync
{
    /// <summary>
    /// SheetSync のビジネスロジックを提供するサービスクラス
    /// コンパイルエラーなしのテスト変更
    /// 
    /// ConvertSetting に対する各種操作（インポート、ダウンロード、コード生成、アセット作成）を
    /// 実行するためのメソッドを提供します。UI層から独立したビジネスロジックを実装しています。
    /// 
    /// 主な責任:
    /// - スプレッドシートからのデータダウンロード
    /// - CSVデータからのコード生成
    /// - ScriptableObject アセットの作成
    /// - インポート処理のオーケストレーション
    /// - 進捗状況の管理とレポート
    /// 
    /// 設計上の特徴:
    /// - 静的メソッドによるステートレスな実装
    /// - コルーチンベースの非同期処理
    /// - エラーハンドリングとロギング
    /// - 拡張可能なアフターインポート処理
    /// </summary>
    public static class SheetSyncService
    {
        private static bool downloadSuccess;
        private static IList<IList<object>> directImportData;
        
        /// <summary>
        /// ConvertSetting のインポート処理を実行します
        /// </summary>
        /// <param name="settings">インポート対象の ConvertSetting</param>
        /// <returns>処理のコルーチン</returns>
        /// <remarks>
        /// このメソッドは以下の処理を順番に実行します：
        /// 1. スプレッドシートからCSVをダウンロード
        /// 2. 必要に応じてコード生成
        /// 3. アセットの作成
        /// 4. アフターインポート処理（追加のインポート、メソッド実行、バリデーション）
        /// </remarks>
        public static IEnumerator ExecuteImport(SheetSync.ConvertSetting settings)
        {
            downloadSuccess = false;
            directImportData = null;
            
            // ダウンロード処理（直接インポートまたはファイル経由）
            if (settings.useDirectImport && settings.useGSPlugin)
            {
                yield return EditorCoroutineRunner.StartCoroutine(ExecuteDirectDownload(settings));
            }
            else
            {
                yield return EditorCoroutineRunner.StartCoroutine(ExecuteDownload(settings));
            }

            if (!downloadSuccess)
            {
                Debug.LogError($"[ExecuteImport] ダウンロード失敗 - {settings.className}");
                yield break;
            }
            
            // 直接インポートの場合はリフレッシュ不要
            if (!settings.useDirectImport)
            {
                // AssetDatabase を明示的にリフレッシュ
                AssetDatabase.Refresh();
                yield return null; // 1フレーム待機
            }

            // データプロバイダーを取得
            GlobalCCSettings gSettings = CCLogic.GetGlobalSettings();
            ICsvDataProvider dataProvider = GetCsvDataProvider(settings, gSettings);
            CreateAssetsJob createAssetsJob = new CreateAssetsJob(settings, dataProvider);
            object generatedAssets = null;

            // Generate Code if type script is not found.
            bool mustGenerateClass = settings.classGenerate && !CodeGenerationService.TryGetTypeWithError(settings.className, out _, settings.checkFullyQualifiedName, dialog: false);
            bool mustGenerateTableClass = settings.tableGenerate && settings.tableClassGenerate && !CodeGenerationService.TryGetTypeWithError(settings.TableClassName, out _, settings.checkFullyQualifiedName, dialog: false);
            
            if (settings.isEnum || mustGenerateClass || mustGenerateTableClass)
            {
                // 直接インポートの場合は、メモリ上のデータからコード生成
                if (settings.useDirectImport && directImportData != null)
                {
                    Debug.Log($"[ExecuteImport] 直接インポートデータからクラス生成 - {settings.className}");
                    
                    // ICsvData を作成
                    var sheetData = new SheetData(directImportData);
                    
                    // コード生成先のディレクトリパスを取得
                    string directoryPath = CCLogic.GetFullPath(settings.GetDirectoryPath(), settings.codeDestination);
                    
                    // ディレクトリが存在しない場合は作成
                    if (!Directory.Exists(directoryPath))
                    {
                        Directory.CreateDirectory(directoryPath);
                        Debug.Log($"Created directory: {directoryPath}");
                    }
                    
                    // 直接データからコード生成
                    CodeGenerationService.GenerateCodeFromData(settings, gSettings, sheetData, directoryPath);
                    
                    // AssetDatabase の更新
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }
                else
                {
                    // 従来のファイルベースのコード生成
                    GenerateOneCode(settings, gSettings);
                }

                if (!settings.isEnum)
                {
                    // コンパイルを待機してからアセットを作成する
                    Debug.Log("Code generated. Waiting for compilation to complete...");
                    
                    // AssetDatabase の更新を強制
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    
                    // コンパイル完了の検出とアセット作成を別のコルーチンで実行
                    EditorApplication.delayCall += () => {
                        EditorCoroutineRunner.StartCoroutine(WaitForCompilationAndCreateAssets(settings, gSettings, createAssetsJob));
                    };
                    
                    yield break;
                }
            }
            // Create Assets
            else
            {
                generatedAssets = createAssetsJob.Execute();
            }

            // AfterImport 処理
            for (int i = 0; i < settings.executeAfterImport.Count; i++)
            {
                var afterSettings = settings.executeAfterImport[i];

                if (afterSettings != null)
                {
                    yield return EditorCoroutineRunner.StartCoroutine(ExecuteImport(afterSettings));
                }
            }

            // AfterImportMethod 処理
            for (int i = 0; i < settings.executeMethodAfterImport.Count; i++)
            {
                var methodName = settings.executeMethodAfterImport[i];

                if (MethodReflection.TryParse(methodName, out var info))
                {
                    info.methodInfo.Invoke(null, new[] { generatedAssets });
                }
                else
                {
                    Debug.LogError($"不正なメソッド名の指定なのでメソッド呼び出しをスキップしました: {methodName}");
                }
            }

            // AfterImport Validation 処理
            if (settings.executeValidationAfterImport.Count > 0)
            {
                bool validationOk = true;

                for (int i = 0; i < settings.executeValidationAfterImport.Count; i++)
                {
                    var methodName = settings.executeValidationAfterImport[i];

                    if (MethodReflection.TryParse(methodName, out var info))
                    {
                        object resultObj = info.methodInfo.Invoke(null, new[] { generatedAssets });

                        if (resultObj is bool resultBool)
                        {
                            validationOk &= resultBool;
                        }
                        else
                        {
                            validationOk = false;
                            Debug.LogError($"Validation Method は bool 値を返す必要があります: {methodName}");
                        }
                    }
                    else
                    {
                        validationOk = false;
                        Debug.LogError($"不正なメソッド名の指定なので Validation メソッド呼び出しをスキップしました: {methodName}");
                    }
                }

                if (validationOk)
                {
                    Debug.Log("<color=green>Validation Success</color>");
                }
                else
                {
                    Debug.LogError("<color=red>Validation Fails...</color>");
                    Debug.LogError("Validation に失敗しました。テーブルを見直して正しいデータに修正してください。");
                }
            }
        }
        
        /// <summary>
        /// コンパイル完了を待機してアセットを作成する
        /// </summary>
        private static IEnumerator WaitForCompilationAndCreateAssets(
            SheetSync.ConvertSetting settings, 
            SheetSync.GlobalCCSettings gSettings, 
            CreateAssetsJob createAssetsJob)
        {
            // コンパイル中の間待機
            while (EditorApplication.isCompiling)
            {
                yield return null;
            }
            
            // 少し待機してからアセット作成を実行
            yield return new WaitForSecondsRealtime(0.5f);
            
            Debug.Log("Creating assets after compilation...");
            
            // アセット作成を実行
            var generatedAssets = createAssetsJob.Execute();
            
            // AfterImport 処理
            for (int i = 0; i < settings.executeAfterImport.Count; i++)
            {
                var afterSettings = settings.executeAfterImport[i];
                if (afterSettings != null)
                {
                    yield return EditorCoroutineRunner.StartCoroutine(ExecuteImport(afterSettings));
                }
            }
            
            // AfterImportMethod 処理
            for (int i = 0; i < settings.executeMethodAfterImport.Count; i++)
            {
                var methodName = settings.executeMethodAfterImport[i];
                if (MethodReflection.TryParse(methodName, out var info))
                {
                    info.methodInfo.Invoke(null, new[] { generatedAssets });
                }
                else
                {
                    Debug.LogError($"不正なメソッド名の指定なので afterImport メソッド呼び出しをスキップしました: {methodName}");
                }
            }
            
            // ValidationMethod 処理
            bool validationOk = true;
            for (int i = 0; i < settings.executeValidationAfterImport.Count; i++)
            {
                var methodName = settings.executeValidationAfterImport[i];
                if (MethodReflection.TryParse(methodName, out var info))
                {
                    object resultObj = info.methodInfo.Invoke(null, new[] { generatedAssets });
                    if (resultObj is bool resultBool)
                    {
                        validationOk &= resultBool;
                    }
                    else
                    {
                        validationOk = false;
                        Debug.LogError($"Validation Method は bool 値を返す必要があります: {methodName}");
                    }
                }
                else
                {
                    validationOk = false;
                    Debug.LogError($"不正なメソッド名の指定なので Validation メソッド呼び出しをスキップしました: {methodName}");
                }
            }
            
            if (validationOk)
            {
                Debug.Log("<color=green>Validation Success</color>");
            }
            else
            {
                Debug.LogError("<color=red>Validation Fails...</color>");
                Debug.LogError("Validation に失敗しました。テーブルを見直して正しいデータに修正してください。");
            }
        }
        
        /// <summary>
        /// ConvertSetting のダウンロード処理を実行します
        /// </summary>
        /// <param name="settings">ダウンロード対象の ConvertSetting</param>
        /// <returns>処理のコルーチン</returns>
        /// <remarks>
        /// Google スプレッドシートから CSV データをダウンロードし、
        /// 指定されたパスに保存します。GSPlugin を使用して通信を行います。
        /// </remarks>
        public static IEnumerator ExecuteDownload(SheetSync.ConvertSetting settings)
        {
            SheetSync.GlobalCCSettings gSettings = SheetSync.CCLogic.GetGlobalSettings();

            string csvPath = settings.GetCsvPath(gSettings);
            if (string.IsNullOrWhiteSpace(csvPath))
            {
                Debug.LogError("unexpected downloadPath: " + csvPath);
                downloadSuccess = false;
                yield break;
            }

            string absolutePath = SheetSync.CCLogic.GetFilePathRelativesToAssets(settings.GetDirectoryPath(), csvPath);

            // 先頭の Assets を削除する
            string targetPath;
            if (absolutePath.StartsWith("Assets" + Path.DirectorySeparatorChar))
            {
                // "Assets/" または "Assets\" の後の部分を取得
                targetPath = absolutePath.Substring(7); // "Assets/" の長さは7
            }
            else if (absolutePath.StartsWith("Assets/"))
            {
                targetPath = absolutePath.Substring(7);
            }
            else
            {
                Debug.LogError("unexpected downloadPath: " + absolutePath);
                downloadSuccess = false;
                yield break;
            }
            
            Debug.Log($"ExecuteDownload - csvPath: {csvPath}");
            Debug.Log($"ExecuteDownload - absolutePath: {absolutePath}");
            Debug.Log($"ExecuteDownload - targetPath: {targetPath}");
            Debug.Log($"ExecuteDownload - outputDirectory: {settings.GetDirectoryPath()}");

            // SheetDownloadInfo を作成
            var sheetInfo = new SheetDownloadInfo(
                targetPath: targetPath,
                sheetId: settings.sheetID,
                gid: settings.gid
            );

            // Google Sheets API v4 を使用してダウンロード
            yield return GoogleSheetsDownloader.DownloadAsCsv(sheetInfo, settings.GetDirectoryPath());

            // 成功判定を行う.
            if (GoogleSheetsDownloader.previousDownloadSuccess)
            {
                downloadSuccess = true;
            }

            yield break;
        }
        
        /// <summary>
        /// ConvertSetting の直接ダウンロード処理を実行します（ファイルを経由しない）
        /// </summary>
        /// <param name="settings">ダウンロード対象の ConvertSetting</param>
        /// <returns>処理のコルーチン</returns>
        /// <remarks>
        /// Google Sheets API からデータを取得し、メモリ上に保持します。
        /// ファイル保存をバイパスしてメモリ効率を向上させます。
        /// </remarks>
        public static IEnumerator ExecuteDirectDownload(SheetSync.ConvertSetting settings)
        {
            // Debug.Log($"[ExecuteDirectDownload] 直接インポート開始 - {settings.className}");
            
            // SheetDownloadInfo を作成
            var sheetInfo = new SheetDownloadInfo(
                targetPath: "", // 直接インポートでは不要
                sheetId: settings.sheetID,
                gid: settings.gid
            );

            // Google Sheets API v4 を使用して直接データ取得
            yield return GoogleSheetsDownloader.DownloadAsData(sheetInfo, verbose: settings.verbose);

            // 成功判定とデータ取得
            if (GoogleSheetsDownloader.previousDownloadSuccess && 
                GoogleSheetsDownloader.previousDownloadData != null)
            {
                directImportData = GoogleSheetsDownloader.previousDownloadData;
                downloadSuccess = true;

                if (settings.verbose)
                {
                    Debug.Log($"[ExecuteDirectDownload] データ取得成功 - 行数: {directImportData.Count}");
                }
            }
            else
            {
                downloadSuccess = false;
                
                // 詳細なエラー情報を表示
                string errorDetails = "[ExecuteDirectDownload] データ取得失敗\n";
                errorDetails += $"- SheetID: {settings.sheetID}\n";
                errorDetails += $"- GID: {settings.gid}\n";
                
                // GoogleSheetsDownloader からの詳細なエラー情報を追加
                if (!string.IsNullOrEmpty(GoogleSheetsDownloader.previousError))
                {
                    errorDetails += $"- エラー詳細: {GoogleSheetsDownloader.previousError}\n";
                }
                else if (!GoogleSheetsDownloader.previousDownloadSuccess)
                {
                    errorDetails += "- ダウンロード処理が失敗しました（詳細不明）\n";
                }
                
                if (GoogleSheetsDownloader.previousDownloadData == null && GoogleSheetsDownloader.previousDownloadSuccess)
                {
                    errorDetails += "- データの取得には成功しましたが、データが空でした\n";
                }
                
                Debug.LogError(errorDetails);
                
                // ユーザーにダイアログで通知（エラー詳細も含める）
                string dialogMessage = $"スプレッドシート '{settings.className}' のデータ取得に失敗しました。\n\n" +
                    $"SheetID: {settings.sheetID}\n" +
                    $"GID: {settings.gid}\n\n";
                
                if (!string.IsNullOrEmpty(GoogleSheetsDownloader.previousError))
                {
                    dialogMessage += $"エラー: {GoogleSheetsDownloader.previousError}\n\n";
                }
                
                dialogMessage += "詳細はConsoleログを確認してください。";
                
                EditorUtility.DisplayDialog(
                    "直接インポート失敗",
                    dialogMessage,
                    "OK"
                );
            }

            yield break;
        }
        
        /// <summary>
        /// すべての ConvertSetting に対してコード生成を実行します
        /// </summary>
        /// <param name="settings">コード生成対象の ConvertSetting 配列</param>
        /// <param name="globalSettings">グローバル設定</param>
        /// <remarks>
        /// 各 ConvertSetting に対して順番にコード生成を実行し、
        /// 進捗状況を表示します。エラーが発生しても処理を継続します。
        /// </remarks>
        public static void GenerateAllCode(SheetSync.ConvertSetting[] settings, SheetSync.GlobalCCSettings globalSettings)
        {
            int i = 0;

            try
            {
                foreach (SheetSync.ConvertSetting s in settings)
                {
                    ShowProgress(s.className, (float)i / settings.Length, i, settings.Length);
                    CodeGenerationService.GenerateCode(s, globalSettings);
                    i++;
                    ShowProgress(s.className, (float)i / settings.Length, i, settings.Length);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.ClearProgressBar();
        }
        
        /// <summary>
        /// すべての ConvertSetting に対してアセット作成を実行します
        /// </summary>
        /// <param name="settings">アセット作成対象の ConvertSetting 配列</param>
        /// <param name="globalSettings">グローバル設定</param>
        /// <remarks>
        /// 各 ConvertSetting に対して ScriptableObject アセットを作成します。
        /// エラーが発生しても処理を継続します。
        /// </remarks>
        public static void CreateAllAssets(SheetSync.ConvertSetting[] settings, SheetSync.GlobalCCSettings globalSettings)
        {
            try
            {
                for (int i = 0; i < settings.Length; i++)
                {
                    CodeGenerationService.CreateAssets(settings[i], globalSettings);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.ClearProgressBar();
        }
        
        /// <summary>
        /// 単一の ConvertSetting に対してコード生成を実行します
        /// </summary>
        /// <param name="settings">コード生成対象の ConvertSetting</param>
        /// <param name="globalSettings">グローバル設定</param>
        /// <remarks>
        /// 指定された ConvertSetting に対してコード生成を実行し、
        /// 進捗状況を表示します。
        /// </remarks>
        public static void GenerateOneCode(SheetSync.ConvertSetting settings, SheetSync.GlobalCCSettings globalSettings)
        {
            ShowProgress(settings.className, 0, 0, 1);

            try
            {
                CodeGenerationService.GenerateCode(settings, globalSettings);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ShowProgress(settings.className, 1, 1, 1);

            EditorUtility.ClearProgressBar();
        }
        
        /// <summary>
        /// 進捗状況を表示します
        /// </summary>
        /// <param name="name">処理中のアイテム名</param>
        /// <param name="progress">進捗率（0.0～1.0）</param>
        /// <param name="current">現在の処理数</param>
        /// <param name="total">全体の処理数</param>
        private static void ShowProgress(string name, float progress, int current, int total)
        {
            EditorUtility.DisplayProgressBar("Progress", ProgressMessage(name, current, total), progress);
        }

        /// <summary>
        /// 進捗メッセージを生成します
        /// </summary>
        /// <param name="name">処理中のアイテム名</param>
        /// <param name="current">現在の処理数</param>
        /// <param name="total">全体の処理数</param>
        /// <returns>フォーマットされた進捗メッセージ</returns>
        private static string ProgressMessage(string name, int current, int total)
        {
            return string.Format("Creating {0} ({1}/{2})", name, current, total);
        }
        
        /// <summary>
        /// ConvertSetting に基づいて適切な CsvDataProvider を取得します
        /// </summary>
        /// <param name="settings">変換設定</param>
        /// <param name="globalSettings">グローバル設定</param>
        /// <returns>ICsvDataProvider インスタンス</returns>
        public static ICsvDataProvider GetCsvDataProvider(SheetSync.ConvertSetting settings, SheetSync.GlobalCCSettings globalSettings)
        {
            if (settings.useDirectImport && settings.useGSPlugin && directImportData != null)
            {
                // 直接インポート: メモリ上のデータを使用
                return new GoogleSheetsCsvDataProvider(directImportData);
            }
            else
            {
                // ファイルベース: 従来の CSV ファイルを使用
                string csvPath = settings.GetCsvPath(globalSettings);
                string absolutePath = SheetSync.CCLogic.GetFilePathRelativesToAssets(settings.GetDirectoryPath(), csvPath);
                return new FileCsvDataProvider(absolutePath, globalSettings);
            }
        }
    }
}
