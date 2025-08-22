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
        private ConvertSetting targetSetting;
        
        // パース結果のプレビュー用
        private string previewSheetId = "";
        private string previewGid = "";
        private bool hasValidPreview = false;
        
        public static void ShowDialog(ConvertSetting setting)
        {
            if (setting == null)
            {
                Debug.LogError("ConvertSettingがnullです");
                return;
            }
            
            var window = GetWindow<GoogleSheetsUrlDialog>(true, "Google Spreadsheet URL入力", true);
            window.minSize = new Vector2(550, 280);
            window.maxSize = new Vector2(650, 350);
            window.targetSetting = setting;
            window.ShowModal();
        }
        
        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            
            EditorGUILayout.LabelField("Google SpreadsheetsのURLを入力してください", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
            
            // URL入力欄
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("URL:", GUILayout.Width(40));
            
            EditorGUI.BeginChangeCheck();
            urlInput = EditorGUILayout.TextField(urlInput);
            if (EditorGUI.EndChangeCheck())
            {
                // URL入力が変更されたらリアルタイムでパース
                UpdatePreview();
            }
            
            // ペーストボタン
            if (GUILayout.Button("ペースト", GUILayout.Width(70)))
            {
                urlInput = EditorGUIUtility.systemCopyBuffer;
                UpdatePreview();
            }
            
            // クリアボタン
            if (GUILayout.Button("クリア", GUILayout.Width(70)))
            {
                urlInput = "";
                ClearPreview();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(10);
            
            // パース結果のプレビュー表示
            if (!string.IsNullOrEmpty(urlInput))
            {
                DrawPreviewSection();
            }
            
            // サンプルURL表示
            EditorGUILayout.HelpBox(
                "例: https://docs.google.com/spreadsheets/d/1eDSiCuI_HLeCV96rZioy_PD85AbNmmqdzrxjjS7sJ_w/edit?gid=1380898534", 
                MessageType.Info);
            
            EditorGUILayout.Space(10);
            
            // 現在の設定値を表示
            if (targetSetting != null && (!string.IsNullOrEmpty(targetSetting.sheetID) || !string.IsNullOrEmpty(targetSetting.gid)))
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("現在の設定値:", EditorStyles.boldLabel);
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.TextField("Sheet ID", targetSetting.sheetID);
                EditorGUILayout.TextField("GID", targetSetting.gid);
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.EndVertical();
            }
            
            GUILayout.FlexibleSpace();
            
            // ボタン
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("キャンセル", GUILayout.Width(100)))
            {
                Close();
            }
            
            EditorGUI.BeginDisabledGroup(!hasValidPreview);
            if (GUILayout.Button("適用", GUILayout.Width(100)))
            {
                ApplyToConvertSetting();
            }
            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(5);
        }
        
        private void DrawPreviewSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("パース結果プレビュー:", EditorStyles.boldLabel);
            
            if (hasValidPreview)
            {
                // 有効なパース結果
                GUI.color = Color.green;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUI.color = Color.white;
                
                EditorGUILayout.LabelField("✓ 有効なURLです", EditorStyles.miniLabel);
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.TextField("Sheet ID", previewSheetId);
                EditorGUILayout.TextField("GID", previewGid);
                EditorGUI.EndDisabledGroup();
                
                EditorGUILayout.EndVertical();
            }
            else
            {
                // 無効なURL
                GUI.color = new Color(1f, 0.5f, 0.5f);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUI.color = Color.white;
                
                EditorGUILayout.LabelField("✗ 無効なURLです", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("Google SpreadsheetsのURLを確認してください", EditorStyles.miniLabel);
                
                EditorGUILayout.EndVertical();
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void UpdatePreview()
        {
            if (string.IsNullOrEmpty(urlInput))
            {
                ClearPreview();
                return;
            }
            
            // URLの妥当性チェック
            if (GoogleSheetsUrlParser.IsValidGoogleSheetsUrl(urlInput))
            {
                // URLを解析
                var sheetInfo = GoogleSheetsUrlParser.ParseUrl(urlInput);
                if (sheetInfo != null && sheetInfo.IsValid)
                {
                    previewSheetId = sheetInfo.SheetId;
                    previewGid = sheetInfo.Gid;
                    hasValidPreview = true;
                    
                    // Debug.Log($"[URL Preview] Sheet ID: {previewSheetId}, GID: {previewGid}");
                }
                else
                {
                    ClearPreview();
                }
            }
            else
            {
                ClearPreview();
            }
            
            Repaint();
        }
        
        private void ClearPreview()
        {
            previewSheetId = "";
            previewGid = "";
            hasValidPreview = false;
        }
        
        private void ApplyToConvertSetting()
        {
            if (targetSetting == null)
            {
                EditorUtility.DisplayDialog("エラー", "ConvertSettingが設定されていません。", "OK");
                Close();
                return;
            }
            
            if (!hasValidPreview)
            {
                EditorUtility.DisplayDialog("エラー", "有効なURLが入力されていません。", "OK");
                return;
            }
            
            // Undoに記録
            Undo.RecordObject(targetSetting, "Apply Google Sheets URL");
            
            // 値を更新
            // Debug.Log($"[URL Dialog] 更新前 - SheetID: {targetSetting.sheetID}, GID: {targetSetting.gid}");
            
            targetSetting.sheetID = previewSheetId;
            targetSetting.gid = previewGid;
            targetSetting.useGSPlugin = true; // Google Sheets プラグインを有効化
            
            // Debug.Log($"[URL Dialog] 更新後 - SheetID: {targetSetting.sheetID}, GID: {targetSetting.gid}");
            
            // 変更をマーク
            EditorUtility.SetDirty(targetSetting);
            
            // アセットデータベースを保存
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            // Debug.Log($"[URL Dialog] Google Sheets設定を適用しました - SheetID: {previewSheetId}, GID: {previewGid}");
            
            // ウィンドウを閉じる
            Close();
        }
        
        private void OnDestroy()
        {
            // クリーンアップ
            targetSetting = null;
        }
    }
}