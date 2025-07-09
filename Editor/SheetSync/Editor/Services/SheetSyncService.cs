using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEditor;
using KoheiUtils;
using KoheiUtils.Reflections;

namespace SheetSync.Editor.Services
{
    /// <summary>
    /// SheetSync のビジネスロジックを提供するサービスクラス
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
        public static IEnumerator ExecuteImport(SheetSync.Models.ConvertSetting settings)
        {
            downloadSuccess = false;
            yield return EditorCoroutineRunner.StartCoroutine(ExecuteDownload(settings));

            if (!downloadSuccess)
            {
                yield break;
            }

            CreateAssetsJob createAssetsJob = new CreateAssetsJob(settings);
            object generatedAssets = null;

            // Generate Code if type script is not found.
            Type assetType;
            if (settings.isEnum || !CsvConvert.TryGetTypeWithError(settings.className, out assetType,
                    settings.checkFullyQualifiedName, dialog: false))
            {
                SheetSync.Models.GlobalCCSettings gSettings = SheetSync.CCLogic.GetGlobalSettings();
                GenerateOneCode(settings, gSettings);

                if (!settings.isEnum)
                {
                    EditorUtility.DisplayDialog(
                        "Code Generated",
                        "Please reimport for creating assets after compiling",
                        "ok"
                    );
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
        /// ConvertSetting のダウンロード処理を実行します
        /// </summary>
        /// <param name="settings">ダウンロード対象の ConvertSetting</param>
        /// <returns>処理のコルーチン</returns>
        /// <remarks>
        /// Google スプレッドシートから CSV データをダウンロードし、
        /// 指定されたパスに保存します。GSPlugin を使用して通信を行います。
        /// </remarks>
        public static IEnumerator ExecuteDownload(SheetSync.Models.ConvertSetting settings)
        {
            GSPluginSettings.Sheet sheet = new GSPluginSettings.Sheet();
            sheet.sheetId = settings.sheetID;
            sheet.gid = settings.gid;

            SheetSync.Models.GlobalCCSettings gSettings = SheetSync.CCLogic.GetGlobalSettings();

            string csvPath = settings.GetCsvPath(gSettings);
            if (string.IsNullOrWhiteSpace(csvPath))
            {
                Debug.LogError("unexpected downloadPath: " + csvPath);
                downloadSuccess = false;
                yield break;
            }

            string absolutePath = SheetSync.CCLogic.GetFilePathRelativesToAssets(settings.GetDirectoryPath(), csvPath);

            // 先頭の Assets を削除する
            if (absolutePath.StartsWith("Assets" + Path.DirectorySeparatorChar))
            {
                sheet.targetPath = absolutePath.Substring(6);
            }
            else
            {
                Debug.LogError("unexpected downloadPath: " + absolutePath);
                downloadSuccess = false;
                yield break;
            }

            sheet.isCsv = true;
            sheet.verbose = false;

            string title = "Google Spreadsheet Loader";
            yield return EditorCoroutineRunner.StartCoroutineWithUI(GSEditorWindow.Download(sheet, settings.GetDirectoryPath()), title, true);

            // 成功判定を行う.
            if (GSEditorWindow.previousDownloadSuccess)
            {
                downloadSuccess = true;
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
        public static void GenerateAllCode(SheetSync.Models.ConvertSetting[] settings, SheetSync.Models.GlobalCCSettings globalSettings)
        {
            int i = 0;

            try
            {
                foreach (SheetSync.Models.ConvertSetting s in settings)
                {
                    ShowProgress(s.className, (float)i / settings.Length, i, settings.Length);
                    CsvConvert.GenerateCode(s, globalSettings);
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
        public static void CreateAllAssets(SheetSync.Models.ConvertSetting[] settings, SheetSync.Models.GlobalCCSettings globalSettings)
        {
            try
            {
                for (int i = 0; i < settings.Length; i++)
                {
                    CsvConvert.CreateAssets(settings[i], globalSettings);
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
        public static void GenerateOneCode(SheetSync.Models.ConvertSetting settings, SheetSync.Models.GlobalCCSettings globalSettings)
        {
            ShowProgress(settings.className, 0, 0, 1);

            try
            {
                CsvConvert.GenerateCode(settings, globalSettings);
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
    }
}