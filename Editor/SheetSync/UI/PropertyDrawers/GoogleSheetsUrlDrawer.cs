using UnityEngine;
using UnityEditor;

#if ODIN_INSPECTOR
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using Sirenix.Utilities;
#endif

namespace SheetSync.Editor
{
#if ODIN_INSPECTOR
    /// <summary>
    /// ConvertSettingのGoogle Sheets URL解析機能を提供するOdin Property Drawer
    /// </summary>
    public class ConvertSettingUrlHelperDrawer : OdinValueDrawer<ConvertSetting>
    {
        private string urlInput = "";
        private bool showUrlHelper = false;
        
        protected override void DrawPropertyLayout(GUIContent label)
        {
            var convertSetting = this.ValueEntry.SmartValue;
            
            // デフォルトのプロパティ描画
            CallNextDrawer(label);
            
            // Google Sheets設定セクションの後にURL Helperを追加
            if (convertSetting.useGSPlugin)
            {
                SirenixEditorGUI.BeginBox();
                SirenixEditorGUI.BeginBoxHeader();
                showUrlHelper = SirenixEditorGUI.Foldout(showUrlHelper, "Google Spreadsheet URL Helper");
                SirenixEditorGUI.EndBoxHeader();
                
                if (SirenixEditorGUI.BeginFadeGroup(this, showUrlHelper))
                {
                    DrawUrlHelper(convertSetting);
                }
                SirenixEditorGUI.EndFadeGroup();
                SirenixEditorGUI.EndBox();
            }
        }
        
        private void DrawUrlHelper(ConvertSetting convertSetting)
        {
            EditorGUILayout.Space(5);
            
            SirenixEditorGUI.InfoMessageBox("スプレッドシートのURLを貼り付けて、自動的にSheetIDとGIDを設定できます。");
            
            EditorGUILayout.BeginHorizontal();
            urlInput = EditorGUILayout.TextField("URL", urlInput);
            
            // ペーストボタン
            if (GUILayout.Button("Paste", GUILayout.Width(60)))
            {
                urlInput = EditorGUIUtility.systemCopyBuffer;
            }
            
            // クリアボタン
            if (GUILayout.Button("Clear", GUILayout.Width(60)))
            {
                urlInput = "";
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(urlInput));
            if (GUILayout.Button("URLから設定を適用", GUILayout.Height(25)))
            {
                ApplyUrlToSettings(convertSetting);
            }
            EditorGUI.EndDisabledGroup();
            
            // サンプルURL
            EditorGUILayout.HelpBox(
                "例: https://docs.google.com/spreadsheets/d/1eDSiCuI_HLeCV96rZioy_PD85AbNmmqdzrxjjS7sJ_w/edit?gid=1380898534", 
                MessageType.Info);
            
            // 現在の設定値を表示（読み取り専用）
            if (!string.IsNullOrEmpty(convertSetting.sheetID) || !string.IsNullOrEmpty(convertSetting.gid))
            {
                EditorGUILayout.Space(5);
                SirenixEditorGUI.BeginBox("現在の設定値");
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.TextField("Sheet ID", convertSetting.sheetID);
                EditorGUILayout.TextField("GID", convertSetting.gid);
                EditorGUI.EndDisabledGroup();
                SirenixEditorGUI.EndBox();
            }
        }
        
        private void ApplyUrlToSettings(ConvertSetting setting)
        {
            if (string.IsNullOrEmpty(urlInput))
            {
                EditorUtility.DisplayDialog("エラー", "URLが入力されていません。", "OK");
                return;
            }
            
            // デバッグログを追加
            Debug.Log($"[URL Helper] 入力URL: {urlInput}");
            
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
                // 更新前の値をログ出力
                Debug.Log($"[URL Helper] 更新前 - SheetID: {setting.sheetID}, GID: {setting.gid}");
                
                Undo.RecordObject(setting, "Apply Google Sheets URL");
                
                setting.sheetID = sheetInfo.SheetId;
                setting.gid = sheetInfo.Gid;
                setting.useGSPlugin = true; // Google Sheets プラグインを有効化も追加
                
                EditorUtility.SetDirty(setting);
                
                // アセットデータベースを強制的に保存
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                
                // 成功メッセージと更新後の値を表示
                Debug.Log($"[URL Helper] 更新後 - SheetID: {setting.sheetID}, GID: {setting.gid}");
                Debug.Log($"[URL Helper] Google Sheets設定を適用しました - SheetID: {sheetInfo.SheetId}, GID: {sheetInfo.Gid}");
                
                // URL入力欄をクリア
                urlInput = "";
                
                // ValueEntry を更新（Odin Inspector固有）
                this.ValueEntry.ApplyChanges();
            }
            else
            {
                EditorUtility.DisplayDialog("エラー", 
                    "URLの解析に失敗しました。\n" +
                    "URLの形式を確認してください。", "OK");
            }
        }
    }
#else
    // Odin Inspectorがない場合の通常のCustomEditor実装
    [CustomEditor(typeof(ConvertSetting))]
    public class ConvertSettingInspector : UnityEditor.Editor
    {
        private string urlInput = "";
        private bool showUrlHelper = false;
        
        public override void OnInspectorGUI()
        {
            var convertSetting = (ConvertSetting)target;
            
            // デフォルトのインスペクターを表示
            DrawDefaultInspector();
            
            // Google Sheets 使用時のみURL Helperを表示
            if (convertSetting.useGSPlugin)
            {
                EditorGUILayout.Space();
                showUrlHelper = EditorGUILayout.Foldout(showUrlHelper, "Google Spreadsheet URL Helper", true);
                
                if (showUrlHelper)
                {
                    EditorGUI.indentLevel++;
                    DrawUrlHelper(convertSetting);
                    EditorGUI.indentLevel--;
                }
            }
        }
        
        private void DrawUrlHelper(ConvertSetting convertSetting)
        {
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
            
            // 現在の設定を表示
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Current Settings:", EditorStyles.boldLabel);
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("Sheet ID", convertSetting.sheetID);
            EditorGUILayout.TextField("GID", convertSetting.gid);
            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.EndVertical();
        }
        
        private void ApplyUrlToSettings(ConvertSetting setting)
        {
            if (string.IsNullOrEmpty(urlInput))
            {
                EditorUtility.DisplayDialog("エラー", "URLが入力されていません。", "OK");
                return;
            }
            
            // デバッグログを追加
            Debug.Log($"[URL Helper] 入力URL: {urlInput}");
            
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
                // 更新前の値をログ出力
                Debug.Log($"[URL Helper] 更新前 - SheetID: {setting.sheetID}, GID: {setting.gid}");
                
                Undo.RecordObject(setting, "Apply Google Sheets URL");
                
                setting.sheetID = sheetInfo.SheetId;
                setting.gid = sheetInfo.Gid;
                setting.useGSPlugin = true; // Google Sheets プラグインを有効化
                
                EditorUtility.SetDirty(setting);
                
                // アセットデータベースを強制的に保存
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                
                // 成功メッセージと更新後の値を表示
                Debug.Log($"[URL Helper] 更新後 - SheetID: {setting.sheetID}, GID: {setting.gid}");
                Debug.Log($"[URL Helper] Google Sheets設定を適用しました - SheetID: {sheetInfo.SheetId}, GID: {sheetInfo.Gid}");
                
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
#endif
}