using System;
using UnityEngine;
using UnityEditor;
using SheetSync.Services.Update;
using SheetSync.Services.Auth;

namespace SheetSync.UI.Windows
{
    /// <summary>
    /// OAuth2認証を使用したスプレッドシート更新機能のUIウィンドウ
    /// </summary>
    public class SheetUpdateOAuth2Window : EditorWindow
    {
        private ConvertSetting _selectedSetting;
        private string _searchFieldName = "humanId";
        private string _searchValue = "1";
        private string _updateFieldName = "name";
        private string _updateValue = "Tanaka";
        private bool _isProcessing = false;
        private string _lastResultMessage = "";
        private bool _isAuthenticating = false;
        
        [MenuItem("Tools/SheetSync/Update Records (OAuth2)")]
        public static void ShowWindow()
        {
            var window = GetWindow<SheetUpdateOAuth2Window>("SheetSync Update OAuth2");
            window.minSize = new Vector2(450, 400);
        }
        
        private void OnGUI()
        {
            EditorGUILayout.LabelField("Sheet Update Tool (OAuth2)", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            // OAuth2認証状態の表示
            DrawAuthenticationSection();
            
            if (!GoogleOAuth2Service.IsAuthenticated)
            {
                EditorGUILayout.HelpBox("OAuth2認証が必要です。上記の「認証を開始」ボタンをクリックしてください。", MessageType.Warning);
                return;
            }
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.Space();
            
            // ConvertSetting選択
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Target Setting:", GUILayout.Width(100));
            _selectedSetting = (ConvertSetting)EditorGUILayout.ObjectField(_selectedSetting, typeof(ConvertSetting), false);
            EditorGUILayout.EndHorizontal();
            
            if (_selectedSetting == null)
            {
                EditorGUILayout.HelpBox("ConvertSettingを選択してください。", MessageType.Info);
                return;
            }
            
            if (!_selectedSetting.useGSPlugin)
            {
                EditorGUILayout.HelpBox("選択されたSettingはGoogle Sheetsプラグインを使用していません。", MessageType.Warning);
                return;
            }
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("検索条件", EditorStyles.boldLabel);
            
            // 検索条件入力
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Field:", GUILayout.Width(50));
            _searchFieldName = EditorGUILayout.TextField(_searchFieldName, GUILayout.Width(100));
            EditorGUILayout.LabelField("=", GUILayout.Width(20));
            _searchValue = EditorGUILayout.TextField(_searchValue, GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("更新内容", EditorStyles.boldLabel);
            
            // 更新内容入力
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Field:", GUILayout.Width(50));
            _updateFieldName = EditorGUILayout.TextField(_updateFieldName, GUILayout.Width(100));
            EditorGUILayout.LabelField("→", GUILayout.Width(20));
            _updateValue = EditorGUILayout.TextField(_updateValue, GUILayout.Width(150));
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            // 実行ボタン
            EditorGUI.BeginDisabledGroup(_isProcessing);
            if (GUILayout.Button("更新を実行", GUILayout.Height(30)))
            {
                ExecuteUpdate();
            }
            EditorGUI.EndDisabledGroup();
            
            // 結果表示
            if (!string.IsNullOrEmpty(_lastResultMessage))
            {
                EditorGUILayout.Space();
                var messageType = _lastResultMessage.Contains("成功") ? MessageType.Info : MessageType.Error;
                EditorGUILayout.HelpBox(_lastResultMessage, messageType);
            }
            
            // サンプル表示
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("使用例:", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("検索: humanId = 1");
            EditorGUILayout.LabelField("更新: name → \"Tanaka\"");
            EditorGUILayout.LabelField("結果: humanId=1の行のnameフィールドが更新されます");
            EditorGUILayout.EndVertical();
        }
        
        private void DrawAuthenticationSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("OAuth2認証状態", EditorStyles.boldLabel);
            
            if (GoogleOAuth2Service.IsAuthenticated)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("✅ 認証済み", EditorStyles.boldLabel);
                if (GUILayout.Button("認証をクリア", GUILayout.Width(100)))
                {
                    GoogleOAuth2Service.ClearAuthentication();
                    Repaint();
                }
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.LabelField("❌ 未認証", EditorStyles.boldLabel);
                
                EditorGUI.BeginDisabledGroup(_isAuthenticating);
                if (GUILayout.Button("認証を開始", GUILayout.Height(25)))
                {
                    StartAuthentication();
                }
                EditorGUI.EndDisabledGroup();
                
                if (_isAuthenticating)
                {
                    EditorGUILayout.HelpBox("ブラウザで認証を行ってください...", MessageType.Info);
                }
                
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("初回設定:", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("1. Google Cloud Consoleでプロジェクトを作成", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("2. Sheets APIを有効化", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("3. OAuth2認証情報を作成（デスクトップアプリ）", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("4. credentials.jsonをダウンロード", EditorStyles.miniLabel);
                
                if (GUILayout.Button("設定方法を開く", GUILayout.Width(120)))
                {
                    Application.OpenURL("https://developers.google.com/sheets/api/quickstart/dotnet");
                }
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private async void StartAuthentication()
        {
            _isAuthenticating = true;
            _lastResultMessage = "";
            Repaint();
            
            try
            {
                // シンプルな認証フローを使用
                bool success = await GoogleOAuth2Service.AuthorizeSimpleAsync();
                
                if (success)
                {
                    _lastResultMessage = "認証に成功しました！";
                    Debug.Log(_lastResultMessage);
                }
                else
                {
                    _lastResultMessage = "認証に失敗しました。";
                    Debug.LogError(_lastResultMessage);
                }
            }
            catch (Exception ex)
            {
                _lastResultMessage = $"認証エラー: {ex.Message}";
                Debug.LogException(ex);
            }
            finally
            {
                _isAuthenticating = false;
                Repaint();
            }
        }
        
        private async void ExecuteUpdate()
        {
            _isProcessing = true;
            _lastResultMessage = "";
            
            try
            {
                // クエリを作成
                var query = new SimpleUpdateQuery<object>
                {
                    FieldName = _searchFieldName,
                    SearchValue = ParseValue(_searchValue),
                    UpdateFieldName = _updateFieldName,
                    UpdateValue = ParseValue(_updateValue)
                };
                
                // サービスを作成して実行
                SheetUpdateOAuth2Service service;
                try
                {
                    service = new SheetUpdateOAuth2Service(_selectedSetting);
                }
                catch (InvalidOperationException ex)
                {
                    _lastResultMessage = $"初期化エラー: {ex.Message}";
                    return;
                }
                
                var result = await service.UpdateSingleRowAsync(query);
                
                // 結果を表示
                if (result.Success)
                {
                    _lastResultMessage = $"更新成功！\n" +
                                       $"更新行数: {result.UpdatedRowCount}\n" +
                                       $"処理時間: {result.ElapsedMilliseconds}ms";
                    
                    if (result.UpdatedRows.Count > 0)
                    {
                        var row = result.UpdatedRows[0];
                        _lastResultMessage += $"\n\nRow {row.RowNumber}:";
                        foreach (var change in row.Changes)
                        {
                            _lastResultMessage += $"\n  {change.Key}: \"{change.Value.OldValue}\" → \"{change.Value.NewValue}\"";
                        }
                    }
                }
                else
                {
                    _lastResultMessage = $"更新失敗: {result.ErrorMessage}";
                    Debug.LogError(_lastResultMessage);
                }
            }
            catch (Exception ex)
            {
                _lastResultMessage = $"エラーが発生しました: {ex.Message}";
                Debug.LogException(ex);
            }
            finally
            {
                _isProcessing = false;
                Repaint();
            }
        }
        
        /// <summary>
        /// 文字列を適切な型に変換
        /// </summary>
        private object ParseValue(string value)
        {
            // 数値として解析を試みる
            if (int.TryParse(value, out int intValue))
            {
                return intValue;
            }
            
            if (double.TryParse(value, out double doubleValue))
            {
                return doubleValue;
            }
            
            if (bool.TryParse(value, out bool boolValue))
            {
                return boolValue;
            }
            
            // それ以外は文字列として返す
            return value;
        }
    }
}