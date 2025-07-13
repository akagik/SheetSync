using UnityEngine;
using UnityEditor;

namespace SheetSync.Editor
{
    /// <summary>
    /// ConvertSettingのカスタムインスペクター
    /// Google SpreadsheetsのURLから自動的にSheetIDとGIDを設定する機能を提供
    /// </summary>
    [CustomEditor(typeof(ConvertSetting))]
    public class ConvertSettingInspector : UnityEditor.Editor
    {
        private string urlInput = "";
        private bool showUrlHelper = true;
        
        public override void OnInspectorGUI()
        {
            var convertSetting = (ConvertSetting)target;
            
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            
            // デフォルトのインスペクターを表示
            DrawDefaultInspector();
            
            // URL Helper セクション
            EditorGUILayout.Space();
            showUrlHelper = EditorGUILayout.Foldout(showUrlHelper, "Google Spreadsheet URL Helper", true);
            
            if (showUrlHelper)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                EditorGUILayout.LabelField("スプレッドシートのURLを貼り付けてください:", EditorStyles.boldLabel);
                EditorGUILayout.Space(5);
                
                // URL入力フィールド
                EditorGUILayout.BeginHorizontal();
                urlInput = EditorGUILayout.TextField("URL", urlInput);
                
                // ペーストボタン
                if (GUILayout.Button("Paste", GUILayout.Width(50)))
                {
                    urlInput = EditorGUIUtility.systemCopyBuffer;
                }
                EditorGUILayout.EndHorizontal();
                
                // 適用ボタン
                EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(urlInput));
                if (GUILayout.Button("URLから設定を適用"))
                {
                    ApplyUrlToSettings(convertSetting);
                }
                EditorGUI.EndDisabledGroup();
                
                // ヘルプメッセージ
                EditorGUILayout.HelpBox(
                    "例: https://docs.google.com/spreadsheets/d/1eDSiCuI_HLeCV96rZioy_PD85AbNmmqdzrxjjS7sJ_w/edit?gid=1380898534", 
                    MessageType.Info);
                
                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
            }
            
            
            // 現在の設定を表示（デバッグ用）
            if (convertSetting.useGSPlugin)
            {
                EditorGUILayout.Space();
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Current Google Sheets Settings:", EditorStyles.boldLabel);
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.TextField("Sheet ID", convertSetting.sheetID);
                EditorGUILayout.TextField("GID", convertSetting.gid);
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.EndVertical();
            }
        }
        
        private void ApplyUrlToSettings(ConvertSetting setting)
        {
            if (string.IsNullOrEmpty(urlInput))
            {
                EditorUtility.DisplayDialog("エラー", "URLが入力されていません。", "OK");
                return;
            }
            
            // URLの妥当性チェック
            if (!GoogleSheetsUrlParser.IsValidGoogleSheetsUrl(urlInput))
            {
                EditorUtility.DisplayDialog("エラー", 
                    "有効なGoogle SpreadsheetsのURLではありません。\n" +
                    "URLの形式を確認してください。", "OK");
                return;
            }
            
            // URLを解析して設定に適用
            var sheetInfo = GoogleSheetsUrlParser.ParseUrl(urlInput);
            if (sheetInfo != null && sheetInfo.IsValid)
            {
                Undo.RecordObject(setting, "Apply Google Sheets URL");
                
                setting.sheetID = sheetInfo.SheetId;
                setting.gid = sheetInfo.Gid;
                setting.useGSPlugin = true; // Google Sheets プラグインを有効化
                
                EditorUtility.SetDirty(setting);
                
                // 成功メッセージ
                Debug.Log($"Google Sheets設定を適用しました - SheetID: {sheetInfo.SheetId}, GID: {sheetInfo.Gid}");
                
                // URL入力欄をクリア
                urlInput = "";
                
                // インスペクターを再描画
                Repaint();
            }
            else
            {
                EditorUtility.DisplayDialog("エラー", 
                    "URLの解析に失敗しました。\n" +
                    "URLの形式を確認してください。", "OK");
            }
        }
    }
}