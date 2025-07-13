using System;
using UnityEngine;
using UnityEditor;
using SheetSync.Services.Update;

namespace SheetSync.UI.Windows
{
    /// <summary>
    /// スプレッドシート更新機能のUIウィンドウ（MVP版）
    /// </summary>
    public class SheetUpdateWindow : EditorWindow
    {
        private ConvertSetting _selectedSetting;
        private string _searchFieldName = "humanId";
        private string _searchValue = "1";
        private string _updateFieldName = "name";
        private string _updateValue = "Tanaka";
        private bool _isProcessing = false;
        private string _lastResultMessage = "";
        
        [MenuItem("Tools/SheetSync/Update Records")]
        public static void ShowWindow()
        {
            var window = GetWindow<SheetUpdateWindow>("SheetSync Update");
            window.minSize = new Vector2(400, 300);
        }
        
        private void OnGUI()
        {
            EditorGUILayout.LabelField("Sheet Update Tool (MVP)", EditorStyles.boldLabel);
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
                EditorGUILayout.HelpBox(_lastResultMessage, MessageType.Info);
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
                var service = new SheetUpdateService(_selectedSetting);
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