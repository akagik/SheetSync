using UnityEngine;
using UnityEditor;

namespace SheetSync
{
    /// <summary>
    /// Google SpreadsheetsのURL入力ダイアログ
    /// </summary>
    public class GoogleSheetsUrlDialog : EditorWindow
    {
        private string urlInput = "";
        public string SheetId { get; private set; }
        public string Gid { get; private set; }
        public bool IsConfirmed { get; private set; }
        
        public static GoogleSheetsUrlDialog ShowDialog()
        {
            var window = GetWindow<GoogleSheetsUrlDialog>(true, "Google Spreadsheet URL入力", true);
            window.minSize = new Vector2(500, 200);
            window.maxSize = new Vector2(600, 250);
            window.ShowModal();
            return window;
        }
        
        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            
            EditorGUILayout.LabelField("Google SpreadsheetsのURLを入力してください", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
            
            // URL入力欄
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("URL:", GUILayout.Width(40));
            urlInput = EditorGUILayout.TextField(urlInput);
            
            // ペーストボタン
            if (GUILayout.Button("ペースト", GUILayout.Width(70)))
            {
                urlInput = EditorGUIUtility.systemCopyBuffer;
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(10);
            
            // サンプルURL表示
            EditorGUILayout.HelpBox(
                "例: https://docs.google.com/spreadsheets/d/1eDSiCuI_HLeCV96rZioy_PD85AbNmmqdzrxjjS7sJ_w/edit?gid=1380898534", 
                MessageType.Info);
            
            EditorGUILayout.Space(20);
            
            // ボタン
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("キャンセル", GUILayout.Width(100)))
            {
                IsConfirmed = false;
                Close();
            }
            
            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(urlInput));
            if (GUILayout.Button("適用", GUILayout.Width(100)))
            {
                if (ProcessUrl())
                {
                    IsConfirmed = true;
                    Close();
                }
            }
            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.EndHorizontal();
        }
        
        private bool ProcessUrl()
        {
            // URLの妥当性チェック
            if (!GoogleSheetsUrlParser.IsValidGoogleSheetsUrl(urlInput))
            {
                EditorUtility.DisplayDialog("エラー", 
                    "有効なGoogle SpreadsheetsのURLではありません。\n" +
                    "URLの形式を確認してください。", "OK");
                return false;
            }
            
            // URLを解析
            var sheetInfo = GoogleSheetsUrlParser.ParseUrl(urlInput);
            if (sheetInfo != null && sheetInfo.IsValid)
            {
                SheetId = sheetInfo.SheetId;
                Gid = sheetInfo.Gid;
                return true;
            }
            else
            {
                EditorUtility.DisplayDialog("エラー", 
                    "URLの解析に失敗しました。\n" +
                    "URLの形式を確認してください。", "OK");
                return false;
            }
        }
        
        private void OnDestroy()
        {
            // ウィンドウが閉じられた時の処理
            if (!IsConfirmed)
            {
                SheetId = null;
                Gid = null;
            }
        }
    }
}