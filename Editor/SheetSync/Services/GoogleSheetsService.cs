#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using SheetSync;

#if !UNITY_EDITOR
// ダミー（このコードはエディタでのみ実行される）
#else
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using SheetSync.Services;
using SheetSync.Services.Auth;
#endif

namespace SheetSync
{
    /// <summary>
    /// Google Sheets API v4 を使用してスプレッドシートを CSV 形式でダウンロードするサービス
    /// </summary>
    public static class GoogleSheetsDownloader
    {
        /// <summary>
        /// 前回のダウンロードが成功したかどうか
        /// </summary>
        public static bool previousDownloadSuccess { get; private set; }
        
        /// <summary>
        /// 前回のダウンロードで取得したデータ（直接インポート用）
        /// </summary>
        public static System.Collections.Generic.IList<System.Collections.Generic.IList<object>> previousDownloadData { get; private set; }
        
        /// <summary>
        /// 前回のダウンロードで発生したエラーの詳細
        /// </summary>
        public static string previousError { get; private set; }

        private const string IMPORT_AUTH_MODE_PREF_KEY = "SheetSync_ImportAuthMode";

        private enum DownloadAuthMode
        {
            ApiKey,
            ServiceAccount
        }
        
        /// <summary>
        /// Google スプレッドシートから CSV をダウンロードします
        /// </summary>
        /// <param name="sheet">ダウンロード対象のシート情報</param>
        /// <param name="outputDirectory">出力先ディレクトリ</param>
        /// <returns>コルーチン</returns>
        public static IEnumerator DownloadAsCsv(SheetDownloadInfo sheet, string outputDirectory)
        {
            previousDownloadSuccess = false;
            previousError = null;
            
            // Google API の利用可能性をチェック
            if (!GoogleApiChecker.CheckAndWarn())
            {
                previousError = "Google Sheets APIが利用できません。Google.Apis.Sheets.v4 パッケージがインストールされているか確認してください。";
                yield break;
            }
            
            if (!TryGetOrPromptApiKey(out var apiKey, out var apiKeyError))
            {
                previousError = apiKeyError;
                yield break;
            }
            
            // 非同期でダウンロード処理を実行
            var downloadTask = DownloadSheetAsCsvAsync(sheet, outputDirectory, apiKey);
            
            while (!downloadTask.IsCompleted)
            {
                yield return null;
            }
            
            if (downloadTask.IsFaulted)
            {
                var baseException = downloadTask.Exception?.GetBaseException();
                previousError = $"ダウンロードエラー: {baseException?.Message}";
                Debug.LogError(previousError);
                EditorUtility.DisplayDialog(
                    "ダウンロードエラー",
                    $"スプレッドシートのダウンロードに失敗しました:\n{baseException?.Message}",
                    "OK"
                );
            }
            else
            {
                previousDownloadSuccess = downloadTask.Result;
                if (previousDownloadSuccess)
                {
                    Debug.Log($"ダウンロード成功: {sheet.SheetId} -> {sheet.TargetPath}");
                    AssetDatabase.Refresh();
                }
            }
        }
        
        /// <summary>
        /// 非同期でスプレッドシートを CSV としてダウンロード
        /// </summary>
        private static async Task<bool> DownloadSheetAsCsvAsync(SheetDownloadInfo sheet, string outputDirectory, string apiKey)
        {
            try
            {
                // Google Sheets Service を作成
                var service = new SheetsService(new BaseClientService.Initializer
                {
                    ApiKey = apiKey,
                    ApplicationName = "SheetSync"
                });
                
                // gid からシート名を取得する（共通化された処理を使用）
                string sheetName = await GoogleSheetsUtility.GetSheetNameFromGidAsync(service, sheet.SheetId, sheet.Gid);
                
                if (string.IsNullOrEmpty(sheetName))
                {
                    Debug.LogError($"シート名の取得に失敗しました。スプレッドシートID: {sheet.SheetId}, GID: {sheet.Gid}");
                    return false;
                }
                
                // データを取得
                var range = $"{sheetName}!A:ZZ"; // 全範囲を取得
                var request = service.Spreadsheets.Values.Get(sheet.SheetId, range);
                var response = await request.ExecuteAsync();
                
                if (response.Values == null || response.Values.Count == 0)
                {
                    Debug.LogError("スプレッドシートにデータがありません。");
                    return false;
                }
                
                // CSV として保存
                // CsvConvert と同じロジックでパスを構築する必要がある
                string outputPath;
                
                // targetPath が絶対パス（/で始まる）の場合
                if (sheet.TargetPath.StartsWith("/"))
                {
                    // Assets/ から始まるパスとして構築
                    outputPath = Path.Combine("Assets", sheet.TargetPath.Substring(1));
                }
                else
                {
                    // 相対パスの場合は outputDirectory と結合
                    // ただし、outputDirectory は "Assets/Tests" のような形式
                    // targetPath は "temp/temp.csv" のような形式
                    // 期待される結果は "Assets/temp/temp.csv"
                    
                    // これは元の GetCsvPath が "/" で始まっているため、絶対パスとして扱われるべき
                    // しかし、SheetSyncService で "/" が削除されているため、ここで復元する必要がある
                    outputPath = Path.Combine("Assets", sheet.TargetPath);
                }
                
                string directory = Path.GetDirectoryName(outputPath);
                
                Debug.Log($"outputDirectory: {outputDirectory}");
                Debug.Log($"sheet.TargetPath: {sheet.TargetPath}");
                Debug.Log($"CSV 保存先（フルパス）: {outputPath}");
                Debug.Log($"ディレクトリ: {directory}");
                
                try
                {
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                }
                catch (UnauthorizedAccessException e)
                {
                    Debug.LogError($"ディレクトリ作成エラー: アクセス権限がありません - {directory}");
                    Debug.LogException(e);
                    return false;
                }
                catch (Exception e)
                {
                    Debug.LogError($"ディレクトリ作成エラー: {e.Message} - {directory}");
                    Debug.LogException(e);
                    return false;
                }
                
                // CSV を書き込み
                try
                {
                    using (var writer = new StreamWriter(outputPath, false, System.Text.Encoding.UTF8))
                {
                    foreach (var row in response.Values)
                    {
                        // 各セルを CSV 形式にエスケープ
                        var csvRow = new System.Collections.Generic.List<string>();
                        foreach (var cell in row)
                        {
                            string cellValue = cell?.ToString() ?? "";
                            
                            // 改行コードを \n に統一（CsvConverter との互換性のため）
                            cellValue = cellValue.Replace("\r\n", "\n");
                            
                            // CSV エスケープ処理
                            if (cellValue.Contains("\"") || cellValue.Contains(",") || cellValue.Contains("\n") || cellValue.Contains("\r"))
                            {
                                // ダブルクォートをエスケープ
                                cellValue = cellValue.Replace("\"", "\"\"");
                                // 改行を\\nにエスケープ（CSVフォーマットのため）
                                cellValue = cellValue.Replace("\n", "\\n");
                                // 全体をダブルクォートで囲む
                                cellValue = "\"" + cellValue + "\"";
                            }
                            
                            csvRow.Add(cellValue);
                        }
                        
                        await writer.WriteLineAsync(string.Join(",", csvRow));
                    }
                }
                
                Debug.Log($"CSV を保存しました: {outputPath}");
                
                // ファイルの存在確認
                string fullPath = Path.GetFullPath(outputPath);
                bool fileExists = System.IO.File.Exists(fullPath);
                Debug.Log($"[GoogleSheetsDownloader] 保存確認 - fullPath: {fullPath}");
                Debug.Log($"[GoogleSheetsDownloader] 保存確認 - File.Exists: {fileExists}");
                
                if (fileExists)
                {
                    var fileInfo = new System.IO.FileInfo(fullPath);
                    Debug.Log($"[GoogleSheetsDownloader] ファイルサイズ: {fileInfo.Length} bytes");
                }
                
                return true;
                }
                catch (UnauthorizedAccessException e)
                {
                    Debug.LogError($"ファイル書き込みエラー: アクセス権限がありません - {outputPath}");
                    Debug.LogException(e);
                    return false;
                }
                catch (DirectoryNotFoundException e)
                {
                    Debug.LogError($"ファイル書き込みエラー: ディレクトリが見つかりません - {outputPath}");
                    Debug.LogException(e);
                    return false;
                }
                catch (IOException e)
                {
                    Debug.LogError($"ファイル書き込みエラー: {e.Message} - {outputPath}");
                    Debug.LogException(e);
                    return false;
                }
            }
            catch (Google.GoogleApiException e)
            {
                Debug.LogError($"Google API エラー: {e.Message}");
                Debug.LogError($"ステータスコード: {e.HttpStatusCode}");
                Debug.LogError($"エラー詳細: {e.Error}");
                
                if (e.HttpStatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    Debug.LogError("API キーが無効か、Google Sheets API が有効になっていません。");
                }
                
                return false;
            }
            catch (Exception e)
            {
                Debug.LogError($"予期しないエラー: {e.Message}");
                Debug.LogException(e);
                return false;
            }
        }
        
        /// <summary>
        /// Google スプレッドシートからデータを直接取得します（ファイル保存なし）
        /// </summary>
        /// <param name="sheet">ダウンロード対象のシート情報</param>
        /// <returns>コルーチン</returns>
        public static IEnumerator DownloadAsData(SheetDownloadInfo sheet, bool verbose = false)
        {
            previousDownloadSuccess = false;
            previousDownloadData = null;
            previousError = null;
            
            if (!EnsureImportAuthMode(out var authMode))
            {
                if (!string.IsNullOrEmpty(previousError))
                {
                    Debug.LogError(previousError);
                }
                yield break;
            }

            SheetsService service = null;
            string credentialSummary = string.Empty;

            if (authMode == DownloadAuthMode.ApiKey)
            {
                if (!TryGetOrPromptApiKey(out var apiKey, out var apiKeyError))
                {
                    previousError = apiKeyError;
                    Debug.LogError(previousError);
                    yield break;
                }

                service = new SheetsService(new BaseClientService.Initializer()
                {
                    ApiKey = apiKey,
                    ApplicationName = "SheetSync"
                });

                credentialSummary = $"APIキー({MaskApiKey(apiKey)})";
            }
            else
            {
                if (!GoogleServiceAccountAuth.IsAuthenticated)
                {
                    var authorizeTask = GoogleServiceAccountAuth.AuthorizeAsync(verbose: true);
                    while (!authorizeTask.IsCompleted)
                    {
                        yield return null;
                    }

                    if (authorizeTask.IsFaulted)
                    {
                        if (authorizeTask.Exception != null)
                        {
                            Debug.LogException(authorizeTask.Exception.GetBaseException());
                        }

                        previousError = "サービスアカウント認証に失敗しました。ログを確認してください。";
                        Debug.LogError(previousError);
                        yield break;
                    }

                    if (!authorizeTask.Result)
                    {
                        previousError = "サービスアカウント認証がキャンセルされました。";
                        Debug.LogError(previousError);
                        yield break;
                    }
                }

                try
                {
                    service = GoogleServiceAccountAuth.GetAuthenticatedService();
                    credentialSummary = "サービスアカウント認証";
                }
                catch (Exception e)
                {
                    previousError = $"サービスアカウントの初期化に失敗しました: {e.Message}";
                    Debug.LogError(previousError);
                    Debug.LogException(e);
                    yield break;
                }
            }

            if (verbose)
            {
                Debug.Log($"[DownloadAsData] 開始 - SheetId: {sheet.SheetId}, Gid: {sheet.Gid} ({credentialSummary})");
            }

            // 非同期ダウンロード処理を実行
            var downloadTask = DownloadAsDataInternalAsync(sheet, service, authMode, credentialSummary);
            
            while (!downloadTask.IsCompleted)
            {
                yield return null;
            }
            
            if (downloadTask.IsFaulted)
            {
                var baseException = downloadTask.Exception?.GetBaseException();
                if (string.IsNullOrEmpty(previousError))
                {
                    previousError = $"ダウンロードエラー: {baseException?.Message}";
                }
                Debug.LogError(previousError);
                previousDownloadSuccess = false;
            }
            else
            {
                previousDownloadSuccess = downloadTask.Result;
                if (previousDownloadSuccess && verbose)
                {
                    Debug.Log($"データ取得成功: {previousDownloadData?.Count ?? 0} 行");
                }
            }
        }
        
        /// <summary>
        /// Google スプレッドシートからデータを直接取得します（非同期版）
        /// </summary>
        private static async Task<bool> DownloadAsDataInternalAsync(
            SheetDownloadInfo sheet,
            SheetsService service,
            DownloadAuthMode authMode,
            string credentialSummary)
        {
            try
            {
                // シート名を取得
                var spreadsheet = await service.Spreadsheets.Get(sheet.SheetId).ExecuteAsync();
                string sheetName = null;
                
                foreach (var sheetInfo in spreadsheet.Sheets)
                {
                    if (sheetInfo.Properties.SheetId.ToString() == sheet.Gid)
                    {
                        sheetName = sheetInfo.Properties.Title;
                        break;
                    }
                }
                
                if (string.IsNullOrEmpty(sheetName))
                {
                    previousError = $"GID '{sheet.Gid}' に対応するシートが見つかりません。スプレッドシート内のシートのGIDを確認してください。";
                    return false;
                }
                
                // データを取得
                var range = $"{sheetName}";
                var request = service.Spreadsheets.Values.Get(sheet.SheetId, range);
                
                var response = await request.ExecuteAsync();
                
                if (response.Values == null || response.Values.Count == 0)
                {
                    previousError = $"スプレッドシート '{sheetName}' にデータがありません。スプレッドシートが空であるか、読み取り範囲にデータが存在しない可能性があります。";
                    return false;
                }
                
                // データを保持
                previousDownloadData = response.Values;
                
                return true;
            }
            catch (Google.GoogleApiException e)
            {
                // Google API エラーの詳細を保存
                if (e.HttpStatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    if (authMode == DownloadAuthMode.ApiKey)
                    {
                        previousError = "Google API アクセスエラー: APIキーが無効か、Sheets APIが有効化されていません。";
                        previousError += $"\n{credentialSummary}";
                    }
                    else
                    {
                        previousError = "Google API アクセスエラー: サービスアカウントに対象スプレッドシートへのアクセス権限がありません。共有設定を確認してください。";
                    }
                }
                else if (e.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    previousError = $"スプレッドシートが見つかりません。\nSheetID: {sheet.SheetId}\nスプレッドシートが公開されているか確認してください。";
                }
                else if (e.HttpStatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    previousError = "Google Sheets APIのレート制限に達しました。しばらく待ってから再試行してください。";
                }
                else
                {
                    previousError = $"Google API エラー ({e.HttpStatusCode}): {e.Message}";
                }
                throw;
            }
            catch (Exception e)
            {
                // その他のエラー
                previousError = $"予期しないエラー: {e.GetType().Name} - {e.Message}";
                throw;
            }
        }
        
        /// <summary>
        /// API キーをクリア
        /// </summary>
        [MenuItem("Tools/SheetSync/Google API/Clear API Key")]
        public static void ClearApiKey()
        {
            EditorPrefs.DeleteKey("SheetSync_ApiKey");
            Debug.Log("API キーをクリアしました。");
        }

        /// <summary>
        /// Import で使用する認証方法の選択をリセット
        /// </summary>
        [MenuItem("Tools/SheetSync/Google API/Clear Import Auth Mode")]
        public static void ClearImportAuthMode()
        {
            EditorPrefs.DeleteKey(IMPORT_AUTH_MODE_PREF_KEY);
            Debug.Log("Import の認証方法の選択をリセットしました。");
        }

        private static bool EnsureImportAuthMode(out DownloadAuthMode mode)
        {
            string storedValue = EditorPrefs.GetString(IMPORT_AUTH_MODE_PREF_KEY, string.Empty);
            if (!string.IsNullOrEmpty(storedValue) && Enum.TryParse(storedValue, out mode))
            {
                return true;
            }

            int selection = EditorUtility.DisplayDialogComplex(
                "Import の認証方法の選択",
                "Open SheetSync の Import で使用する認証方法を選択してください。",
                "API キー",
                "キャンセル",
                "サービスアカウント"
            );

            if (selection == 1)
            {
                previousError = "インポート処理がキャンセルされました。";
                mode = default;
                return false;
            }

            mode = selection == 2 ? DownloadAuthMode.ServiceAccount : DownloadAuthMode.ApiKey;
            EditorPrefs.SetString(IMPORT_AUTH_MODE_PREF_KEY, mode.ToString());
            return true;
        }

        private static bool TryGetOrPromptApiKey(out string apiKey, out string errorMessage)
        {
            apiKey = EditorPrefs.GetString("SheetSync_ApiKey", string.Empty);
            errorMessage = null;

            if (!string.IsNullOrEmpty(apiKey))
            {
                return true;
            }

            int dialogResult = EditorUtility.DisplayDialogComplex(
                "API キーが必要です",
                "Google Sheets API を使用するには API キーが必要です。\nAPI キーを入力してください。",
                "入力する",
                "キャンセル",
                "ヘルプ"
            );

            if (dialogResult == 1)
            {
                errorMessage = "APIキーの入力がキャンセルされました。";
                return false;
            }

            if (dialogResult == 2)
            {
                Application.OpenURL("https://console.cloud.google.com/apis/credentials");
                errorMessage = "ヘルプページを開きました。APIキーを取得してから再度お試しください。";
                return false;
            }

            apiKey = EditorInputDialog.Show("API キー入力", "Google API キーを入力してください:", string.Empty);
            if (string.IsNullOrEmpty(apiKey))
            {
                errorMessage = "APIキーが入力されませんでした。";
                return false;
            }

            EditorPrefs.SetString("SheetSync_ApiKey", apiKey);
            return true;
        }

        private static string MaskApiKey(string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                return "未設定";
            }

            if (apiKey.Length <= 8)
            {
                return apiKey;
            }

            return $"{apiKey.Substring(0, 4)}...{apiKey.Substring(apiKey.Length - 4)}";
        }
    }
}
#endif
