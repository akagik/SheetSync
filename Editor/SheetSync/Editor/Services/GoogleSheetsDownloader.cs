#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using SheetSync.Models;

#if !UNITY_EDITOR
// ダミー（このコードはエディタでのみ実行される）
#else
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
#endif

namespace SheetSync.Editor.Services
{
    /// <summary>
    /// ダウンロード対象のシート情報
    /// </summary>
    public class SheetDownloadInfo
    {
        public string TargetPath { get; set; }
        public string SheetId { get; set; }
        public string Gid { get; set; }
        
        public SheetDownloadInfo(string targetPath, string sheetId, string gid)
        {
            TargetPath = targetPath;
            SheetId = sheetId;
            Gid = gid;
        }
    }
    
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
        /// Google スプレッドシートから CSV をダウンロードします
        /// </summary>
        /// <param name="sheet">ダウンロード対象のシート情報</param>
        /// <param name="outputDirectory">出力先ディレクトリ</param>
        /// <returns>コルーチン</returns>
        public static IEnumerator DownloadAsCsv(SheetDownloadInfo sheet, string outputDirectory)
        {
            previousDownloadSuccess = false;
            
            // Google API の利用可能性をチェック
            if (!Utils.GoogleApiChecker.CheckAndWarn())
            {
                yield break;
            }
            
            // API キーを取得
            string apiKey = EditorPrefs.GetString("SheetSync_ApiKey", "");
            
            if (string.IsNullOrEmpty(apiKey))
            {
                int dialogResult = EditorUtility.DisplayDialogComplex(
                    "API キーが必要です",
                    "Google Sheets API を使用するには API キーが必要です。\n" +
                    "API キーを入力してください。",
                    "入力する",
                    "キャンセル",
                    "ヘルプ"
                );
                
                if (dialogResult == 1) // キャンセル
                {
                    yield break;
                }
                else if (dialogResult == 2) // ヘルプ
                {
                    Application.OpenURL("https://console.cloud.google.com/apis/credentials");
                    yield break;
                }
                
                // API キーを入力ダイアログで取得
                apiKey = Tests.EditorInputDialog.Show("API キー入力", "Google API キーを入力してください:", "");
                if (!string.IsNullOrEmpty(apiKey))
                {
                    EditorPrefs.SetString("SheetSync_ApiKey", apiKey);
                }
                else
                {
                    yield break;
                }
            }
            
            // 非同期でダウンロード処理を実行
            var downloadTask = DownloadSheetAsCsvAsync(sheet, outputDirectory, apiKey);
            
            while (!downloadTask.IsCompleted)
            {
                yield return null;
            }
            
            if (downloadTask.IsFaulted)
            {
                Debug.LogError($"ダウンロードエラー: {downloadTask.Exception?.GetBaseException().Message}");
                EditorUtility.DisplayDialog(
                    "ダウンロードエラー",
                    $"スプレッドシートのダウンロードに失敗しました:\n{downloadTask.Exception?.GetBaseException().Message}",
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
                
                // スプレッドシートの情報を取得
                var spreadsheet = await service.Spreadsheets.Get(sheet.SheetId).ExecuteAsync();
                
                // シート名を特定（gid から）
                string sheetName = null;
                foreach (var s in spreadsheet.Sheets)
                {
                    // gid を int に変換
                    if (int.TryParse(sheet.Gid, out int gidInt) && s.Properties.SheetId == gidInt)
                    {
                        sheetName = s.Properties.Title;
                        break;
                    }
                }
                
                if (string.IsNullOrEmpty(sheetName))
                {
                    // gid が見つからない場合は最初のシートを使用
                    sheetName = spreadsheet.Sheets[0].Properties.Title;
                    Debug.LogWarning($"指定された gid {sheet.Gid} が見つかりません。最初のシート '{sheetName}' を使用します。");
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
                            
                            // CSV エスケープ処理
                            if (cellValue.Contains("\"") || cellValue.Contains(",") || cellValue.Contains("\n") || cellValue.Contains("\r"))
                            {
                                cellValue = "\"" + cellValue.Replace("\"", "\"\"") + "\"";
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
        public static IEnumerator DownloadAsData(SheetDownloadInfo sheet)
        {
            previousDownloadSuccess = false;
            previousDownloadData = null;
            
            // メインスレッドで API キーを取得
            string apiKey = EditorPrefs.GetString("SheetSync_ApiKey", "");
            if (string.IsNullOrEmpty(apiKey))
            {
                Debug.LogError("API キーが設定されていません。ファイルベースのダウンロードを先に実行して API キーを設定してください。");
                yield break;
            }
            
            Debug.Log($"[DownloadAsData] 開始 - SheetId: {sheet.SheetId}, Gid: {sheet.Gid}");
            
            // 非同期ダウンロード処理を実行
            var downloadTask = DownloadAsDataInternalAsync(sheet, apiKey);
            
            while (!downloadTask.IsCompleted)
            {
                yield return null;
            }
            
            if (downloadTask.IsFaulted)
            {
                Debug.LogError($"ダウンロードエラー: {downloadTask.Exception?.GetBaseException().Message}");
                previousDownloadSuccess = false;
            }
            else
            {
                previousDownloadSuccess = downloadTask.Result;
                if (previousDownloadSuccess)
                {
                    Debug.Log($"データ取得成功: {previousDownloadData?.Count ?? 0} 行");
                }
            }
        }
        
        /// <summary>
        /// Google スプレッドシートからデータを直接取得します（非同期版）
        /// </summary>
        private static async Task<bool> DownloadAsDataInternalAsync(SheetDownloadInfo sheet, string apiKey)
        {
            try
            {
                
                // Google Sheets API のサービスを作成
                var service = new SheetsService(new BaseClientService.Initializer()
                {
                    ApiKey = apiKey,
                    ApplicationName = "SheetSync"
                });
                
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
                    // エラーメッセージは呼び出し元で表示
                    return false;
                }
                
                // データを取得
                var range = $"{sheetName}";
                var request = service.Spreadsheets.Values.Get(sheet.SheetId, range);
                
                var response = await request.ExecuteAsync();
                
                if (response.Values == null || response.Values.Count == 0)
                {
                    // 警告メッセージは呼び出し元で表示
                    return false;
                }
                
                // データを保持
                previousDownloadData = response.Values;
                
                return true;
            }
            catch (Exception)
            {
                // エラー処理は呼び出し元で行う
                throw;
            }
        }
        
        /// <summary>
        /// API キーをクリア
        /// </summary>
        [MenuItem("SheetSync/Google API/Clear API Key")]
        public static void ClearApiKey()
        {
            EditorPrefs.DeleteKey("SheetSync_ApiKey");
            Debug.Log("API キーをクリアしました。");
        }
    }
}
#endif