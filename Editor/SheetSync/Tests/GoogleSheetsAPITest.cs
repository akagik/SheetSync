#if UNITY_EDITOR
using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using SheetSync;

// Google API が利用可能な場合のみ using
#if !UNITY_EDITOR
// ダミー（このコードはエディタでのみ実行される）
#else
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
#endif

namespace SheetSync.Tests
{
    /// <summary>
    /// Google Sheets API v4 の単体テストクラス
    /// </summary>
    public static class GoogleSheetsAPITest
    {
        // Google が提供している公開サンプルスプレッドシート
        private const string PUBLIC_SAMPLE_SPREADSHEET_ID = "1BxiMVs0XRA5nFMdKvBdBZjgmUUqptlbs74OgvE2upms";
        
        [MenuItem("SheetSync/Tests/Google API/Simple Connection Test")]
        public static async void TestSimpleConnection()
        {
            // Google API の利用可能性をチェック
            if (!GoogleApiChecker.CheckAndWarn())
            {
                return;
            }
            
            Debug.Log("=== Google Sheets API v4 接続テスト開始 ===");
            
            try
            {
                // API キーを取得（EditorPrefs から）
                string apiKey = EditorPrefs.GetString("SheetSync_TestApiKey", "");
                
                if (string.IsNullOrEmpty(apiKey))
                {
                    int dialogResult = EditorUtility.DisplayDialogComplex(
                        "API キーが必要です",
                        "Google Sheets API をテストするには API キーが必要です。\n" +
                        "API キーを入力してください。",
                        "入力する",
                        "キャンセル",
                        "ヘルプ"
                    );
                    
                    if (dialogResult == 1) // キャンセル
                    {
                        return;
                    }
                    else if (dialogResult == 2) // ヘルプ
                    {
                        Application.OpenURL("https://console.cloud.google.com/apis/credentials");
                        return;
                    }
                    
                    // API キーを入力ダイアログで取得
                    apiKey = EditorInputDialog.Show("API キー入力", "Google API キーを入力してください:", "");
                    if (!string.IsNullOrEmpty(apiKey))
                    {
                        EditorPrefs.SetString("SheetSync_TestApiKey", apiKey);
                    }
                }
                
                // Google Sheets Service を作成
                var service = new SheetsService(new BaseClientService.Initializer
                {
                    ApiKey = apiKey,
                    ApplicationName = "SheetSync Test"
                });
                
                Debug.Log("✓ Google Sheets Service 作成成功");
                
                // テスト: スプレッドシートのメタデータを取得
                var spreadsheetRequest = service.Spreadsheets.Get(PUBLIC_SAMPLE_SPREADSHEET_ID);
                var spreadsheet = await Task.Run(() => spreadsheetRequest.Execute());
                
                Debug.Log($"✓ スプレッドシート取得成功: {spreadsheet.Properties.Title}");
                Debug.Log($"  シート数: {spreadsheet.Sheets.Count}");
                
                // テスト: 最初のシートからデータを読み取り
                string range = "Class Data!A1:E5";
                var valuesRequest = service.Spreadsheets.Values.Get(PUBLIC_SAMPLE_SPREADSHEET_ID, range);
                var response = await Task.Run(() => valuesRequest.Execute());
                
                if (response.Values != null && response.Values.Count > 0)
                {
                    Debug.Log($"✓ データ読み取り成功: {response.Values.Count} 行");
                    
                    // 最初の数行を表示
                    for (int i = 0; i < Math.Min(3, response.Values.Count); i++)
                    {
                        var row = response.Values[i];
                        Debug.Log($"  Row {i}: {string.Join(", ", row)}");
                    }
                }
                
                Debug.Log("=== テスト完了 ===");
                EditorUtility.DisplayDialog("成功", "Google Sheets API への接続に成功しました！", "OK");
            }
            catch (Google.GoogleApiException e)
            {
                Debug.LogError($"Google API エラー: {e.Message}");
                Debug.LogError($"ステータスコード: {e.HttpStatusCode}");
                Debug.LogError($"エラー詳細: {e.Error}");
                
                EditorUtility.DisplayDialog(
                    "Google API エラー",
                    $"エラーが発生しました:\n{e.Message}\n\n" +
                    "API キーが正しいか、Google Sheets API が有効になっているか確認してください。",
                    "OK"
                );
            }
            catch (Exception e)
            {
                Debug.LogError($"予期しないエラー: {e.Message}");
                Debug.LogException(e);
                
                EditorUtility.DisplayDialog("エラー", $"予期しないエラーが発生しました:\n{e.Message}", "OK");
            }
        }
        
        [MenuItem("SheetSync/Tests/Google API/Test Spreadsheet by ID")]
        public static async void TestSpreadsheetById()
        {
            string spreadsheetId = EditorInputDialog.Show(
                "スプレッドシート ID",
                "テストするスプレッドシート ID を入力してください:",
                PUBLIC_SAMPLE_SPREADSHEET_ID
            );
            
            if (string.IsNullOrEmpty(spreadsheetId))
                return;
            
            await TestSpecificSpreadsheet(spreadsheetId);
        }
        
        private static async Task TestSpecificSpreadsheet(string spreadsheetId)
        {
            Debug.Log($"=== スプレッドシート {spreadsheetId} のテスト開始 ===");
            
            try
            {
                string apiKey = EditorPrefs.GetString("SheetSync_TestApiKey", "");
                if (string.IsNullOrEmpty(apiKey))
                {
                    Debug.LogError("API キーが設定されていません。先に Simple Connection Test を実行してください。");
                    return;
                }
                
                var service = new SheetsService(new BaseClientService.Initializer
                {
                    ApiKey = apiKey,
                    ApplicationName = "SheetSync Test"
                });
                
                // スプレッドシート情報を取得
                var spreadsheet = await Task.Run(() => service.Spreadsheets.Get(spreadsheetId).Execute());
                
                Debug.Log($"スプレッドシート名: {spreadsheet.Properties.Title}");
                Debug.Log("シート一覧:");
                
                foreach (var sheet in spreadsheet.Sheets)
                {
                    var props = sheet.Properties;
                    Debug.Log($"  - {props.Title} (ID: {props.SheetId})");
                }
                
                // 最初のシートからサンプルデータを取得
                if (spreadsheet.Sheets.Count > 0)
                {
                    var firstSheet = spreadsheet.Sheets[0].Properties.Title;
                    var range = $"{firstSheet}!A1:Z10";
                    
                    var values = await Task.Run(() => 
                        service.Spreadsheets.Values.Get(spreadsheetId, range).Execute()
                    );
                    
                    if (values.Values != null)
                    {
                        Debug.Log($"\n'{firstSheet}' の最初の10行:");
                        foreach (var row in values.Values)
                        {
                            Debug.Log("  " + string.Join(" | ", row));
                        }
                    }
                }
                
                Debug.Log("=== テスト完了 ===");
            }
            catch (Exception e)
            {
                Debug.LogError($"エラー: {e.Message}");
                Debug.LogException(e);
            }
        }
        
        [MenuItem("SheetSync/Tests/Google API/Clear API Key")]
        public static void ClearApiKey()
        {
            EditorPrefs.DeleteKey("SheetSync_TestApiKey");
            Debug.Log("API キーをクリアしました。");
        }
    }
    
    /// <summary>
    /// 簡易入力ダイアログ
    /// </summary>
    public class EditorInputDialog : EditorWindow
    {
        private static string inputValue = "";
        private static bool shouldClose = false;
        private static string message = "";
        
        public static string Show(string title, string message, string defaultValue = "")
        {
            inputValue = defaultValue;
            shouldClose = false;
            EditorInputDialog.message = message;
            
            var window = GetWindow<EditorInputDialog>(true, title, true);
            window.minSize = new Vector2(400, 100);
            window.maxSize = new Vector2(400, 100);
            window.ShowModal();
            
            while (!shouldClose)
            {
                System.Threading.Thread.Sleep(50);
            }
            
            return inputValue;
        }
        
        void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField(message);
            EditorGUILayout.Space(5);
            
            inputValue = EditorGUILayout.TextField(inputValue);
            
            EditorGUILayout.Space(10);
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("OK", GUILayout.Width(80)))
            {
                shouldClose = true;
                Close();
            }
            
            if (GUILayout.Button("キャンセル", GUILayout.Width(80)))
            {
                inputValue = "";
                shouldClose = true;
                Close();
            }
            
            EditorGUILayout.EndHorizontal();
        }
        
        void OnDestroy()
        {
            shouldClose = true;
        }
    }
}
#endif