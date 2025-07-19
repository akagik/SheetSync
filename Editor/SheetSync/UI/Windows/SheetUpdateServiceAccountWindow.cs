using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using SheetSync.Services.Auth;
using SheetSync.Services.Update;
using SheetSync.Services;
using KoheiUtils;

namespace SheetSync.UI.Windows
{
    /// <summary>
    /// サービスアカウント認証を使用したスプレッドシート更新ウィンドウ
    /// </summary>
    public class SheetUpdateServiceAccountWindow : EditorWindow
    {
        private SheetUpdateServiceAccountService _updateService;
        private ConvertSetting _selectedSetting;
        
        // 検索条件
        private string _keyColumn = "humanId";
        private string _keyValue = "1";
        
        // 更新データ
        private string _updateColumn = "name";
        private string _updateValue = "Tanaka";
        
        // UI状態
        private bool _isUpdating = false;
        private string _statusMessage = "";
        
        [MenuItem("Tools/SheetSync/Update Records (Service Account)")]
        public static void ShowWindow()
        {
            var window = GetWindow<SheetUpdateServiceAccountWindow>("Sheet Update (Service Account)");
            window.minSize = new Vector2(500, 400);
            window.Show();
        }
        
        private void OnEnable()
        {
            _updateService = new SheetUpdateServiceAccountService();
        }
        
        private void OnGUI()
        {
            DrawHeader();
            DrawAuthenticationSection();
            
            if (GoogleServiceAccountAuth.IsAuthenticated)
            {
                DrawUpdateSection();
                DrawStatusSection();
            }
        }
        
        private void DrawHeader()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Google Sheets 更新 (サービスアカウント)", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
        }
        
        private void DrawAuthenticationSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("認証状態", EditorStyles.boldLabel);
            
            if (GoogleServiceAccountAuth.IsAuthenticated)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("✓ サービスアカウント認証済み", EditorStyles.miniLabel);
                if (GUILayout.Button("認証をクリア", GUILayout.Width(100)))
                {
                    GoogleServiceAccountAuth.ClearAuthentication();
                    Repaint();
                }
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.LabelField("認証が必要です", EditorStyles.miniLabel);
                
                if (GUILayout.Button("認証を開始", GUILayout.Height(30)))
                {
                    StartServiceAccountAuth();
                }
                
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox(
                    "サービスアカウントキー（JSON）が必要です。\n" +
                    "ProjectSettings/SheetSync/service-account-key.json に配置してください。",
                    MessageType.Info);
            }
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10);
        }
        
        private void DrawUpdateSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("更新設定", EditorStyles.boldLabel);
            
            // ConvertSetting選択
            _selectedSetting = (ConvertSetting)EditorGUILayout.ObjectField(
                "Convert Setting", 
                _selectedSetting, 
                typeof(ConvertSetting), 
                false);
            
            if (_selectedSetting == null)
            {
                EditorGUILayout.HelpBox("ConvertSettingを選択してください", MessageType.Warning);
                EditorGUILayout.EndVertical();
                return;
            }
            
            EditorGUILayout.Space(10);
            
            // 検索条件
            EditorGUILayout.LabelField("検索条件", EditorStyles.miniBoldLabel);
            _keyColumn = EditorGUILayout.TextField("キー列名", _keyColumn);
            _keyValue = EditorGUILayout.TextField("検索値", _keyValue);
            
            EditorGUILayout.Space(10);
            
            // 更新内容
            EditorGUILayout.LabelField("更新内容", EditorStyles.miniBoldLabel);
            _updateColumn = EditorGUILayout.TextField("更新する列名", _updateColumn);
            _updateValue = EditorGUILayout.TextField("新しい値", _updateValue);
            
            EditorGUILayout.Space(10);
            
            // 更新ボタン
            EditorGUI.BeginDisabledGroup(_isUpdating || string.IsNullOrEmpty(_keyColumn) || string.IsNullOrEmpty(_keyValue));
            if (GUILayout.Button("更新を実行", GUILayout.Height(30)))
            {
                ExecuteUpdate();
            }
            EditorGUI.EndDisabledGroup();
            
            // サービスアカウント特有の注意事項
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox(
                "重要: スプレッドシートをサービスアカウントのメールアドレスと共有する必要があります。\n" +
                "サービスアカウントのメールアドレスは service-account-key.json 内の client_email です。",
                MessageType.Warning);
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawStatusSection()
        {
            if (!string.IsNullOrEmpty(_statusMessage))
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                var messageType = _statusMessage.Contains("エラー") ? MessageType.Error : MessageType.Info;
                EditorGUILayout.HelpBox(_statusMessage, messageType);
                
                EditorGUILayout.EndVertical();
            }
        }
        
        private async void StartServiceAccountAuth()
        {
            try
            {
                _statusMessage = "サービスアカウント認証中...";
                Repaint();
                
                var success = await GoogleServiceAccountAuth.AuthorizeAsync();
                
                if (success)
                {
                    _statusMessage = "サービスアカウント認証に成功しました！";
                }
                else
                {
                    _statusMessage = "サービスアカウント認証に失敗しました。設定を確認してください。";
                }
                
                Repaint();
            }
            catch (Exception ex)
            {
                _statusMessage = $"認証エラー: {ex.Message}";
                Debug.LogException(ex);
                Repaint();
            }
        }
        
        private async void ExecuteUpdate()
        {
            if (_updateService == null || _selectedSetting == null)
                return;
            
            _isUpdating = true;
            _statusMessage = "更新中...";
            Repaint();
            
            try
            {
                var updateData = new Dictionary<string, object>
                {
                    { _updateColumn, _updateValue }
                };
                
                // gidからシート名を取得
                var service = GoogleServiceAccountAuth.GetAuthenticatedService();
                var sheetName = await GoogleSheetsUtility.GetSheetNameFromSettingAsync(service, _selectedSetting);
                
                if (string.IsNullOrEmpty(sheetName))
                {
                    _statusMessage = "シート名の取得に失敗しました。ConvertSettingの設定を確認してください。";
                    return;
                }
                
                var success = await _updateService.UpdateRowAsync(
                    _selectedSetting.sheetID,
                    sheetName,
                    _keyColumn,
                    _keyValue,
                    updateData
                );
                
                if (success)
                {
                    _statusMessage = $"更新成功: {_keyColumn}='{_keyValue}' の {_updateColumn} を '{_updateValue}' に更新しました。";
                }
                else
                {
                    _statusMessage = "更新に失敗しました。ログを確認してください。";
                }
            }
            catch (Exception ex)
            {
                _statusMessage = $"更新エラー: {ex.Message}";
                Debug.LogException(ex);
            }
            finally
            {
                _isUpdating = false;
                Repaint();
            }
        }
    }
}