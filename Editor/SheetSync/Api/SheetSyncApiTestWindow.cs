using UnityEngine;
using UnityEditor;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SheetSync.Api
{
    /// <summary>
    /// SheetSyncApiのテスト用EditorWindow
    /// </summary>
    public class SheetSyncApiTestWindow : EditorWindow
    {
        private string spreadsheetId = "1eDSiCuI_HLeCV96rZioy_PD85AbNmmqdzrxjjS7sJ_w";
        private string gid = "1380898534";
        private string keyColumn = "humanId";
        private string keyValue = "2";
        private string updateKey = "age";
        private string updateValue = "999";
        
        private string lastResult = "";
        private Vector2 scrollPosition;
        
        [MenuItem("Tools/SheetSync/API Test Window")]
        static void Init()
        {
            var window = GetWindow<SheetSyncApiTestWindow>("SheetSync API Test");
            window.Show();
        }
        
        void OnGUI()
        {
            EditorGUILayout.LabelField("SheetSync API Test", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            // 認証テスト
            EditorGUILayout.LabelField("認証", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Initialize Auth"))
            {
                TestInitializeAuth();
            }
            
            if (GUILayout.Button("Check Auth Status"))
            {
                TestCheckAuthStatus();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            // 更新テスト
            EditorGUILayout.LabelField("データ更新", EditorStyles.boldLabel);
            
            spreadsheetId = EditorGUILayout.TextField("Spreadsheet ID", spreadsheetId);
            gid = EditorGUILayout.TextField("GID", gid);
            keyColumn = EditorGUILayout.TextField("Key Column", keyColumn);
            keyValue = EditorGUILayout.TextField("Key Value", keyValue);
            updateKey = EditorGUILayout.TextField("Update Key", updateKey);
            updateValue = EditorGUILayout.TextField("Update Value", updateValue);
            
            EditorGUILayout.Space();
            
            if (GUILayout.Button("Test UpdateRow (Async)"))
            {
                TestUpdateRowAsync();
            }
            
            EditorGUILayout.Space();
            
            // シート名取得テスト
            EditorGUILayout.LabelField("ユーティリティ", EditorStyles.boldLabel);
            
            if (GUILayout.Button("Test Get Sheet Name from GID"))
            {
                TestGetSheetName();
            }
            
            EditorGUILayout.Space();
            
            // 結果表示
            EditorGUILayout.LabelField("実行結果:", EditorStyles.boldLabel);
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));
            EditorGUILayout.TextArea(lastResult, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
            
            if (GUILayout.Button("Clear"))
            {
                lastResult = "";
            }
        }
        
        private void TestInitializeAuth()
        {
            AddLog("=== Test InitializeAuth ===");
            try
            {
                var result = SheetSyncApi.InitializeAuth("");
                AddLog($"Result: {result}");
            }
            catch (System.Exception ex)
            {
                AddLog($"Error: {ex.Message}");
                Debug.LogException(ex);
            }
        }
        
        private void TestCheckAuthStatus()
        {
            AddLog("=== Test CheckAuthStatus ===");
            try
            {
                var result = SheetSyncApi.CheckAuthStatus();
                AddLog($"Result: {result}");
            }
            catch (System.Exception ex)
            {
                AddLog($"Error: {ex.Message}");
                Debug.LogException(ex);
            }
        }
        
        private async void TestUpdateRowAsync()
        {
            AddLog("=== Test UpdateRow (Async) ===");
            try
            {
                var request = new SheetSyncApi.UpdateRequest
                {
                    spreadsheetId = spreadsheetId,
                    gid = gid,
                    keyColumn = keyColumn,
                    keyValue = keyValue,
                    updateData = new System.Collections.Generic.Dictionary<string, object>
                    {
                        [updateKey] = updateValue
                    }
                };
                
                var requestJson = JsonConvert.SerializeObject(request);
                AddLog($"Request: {requestJson}");
                
                AddLog("Calling UpdateRowAsync...");
                var result = await SheetSyncApi.UpdateRowAsync(requestJson);
                AddLog($"Result: {result}");
            }
            catch (System.Exception ex)
            {
                AddLog($"Error: {ex.Message}");
                Debug.LogException(ex);
            }
        }
        
        private void TestGetSheetName()
        {
            AddLog("=== Test Get Sheet Name from GID ===");
            try
            {
                var sheetName = SheetSyncApiHelper.TestGetSheetNameFromGid(spreadsheetId, gid);
                AddLog($"Sheet Name: {sheetName}");
            }
            catch (System.Exception ex)
            {
                AddLog($"Error: {ex.Message}");
                Debug.LogException(ex);
            }
        }
        
        private void AddLog(string message)
        {
            lastResult += $"{System.DateTime.Now:HH:mm:ss} {message}\n";
            Debug.Log($"[SheetSyncApiTest] {message}");
        }
    }
}