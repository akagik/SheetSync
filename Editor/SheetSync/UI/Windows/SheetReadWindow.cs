using System;
using System.Text;
using UnityEngine;
using UnityEditor;
using SheetSync.Services.Update;

namespace SheetSync.UI.Windows
{
    /// <summary>
    /// スプレッドシート読み取り機能のUIウィンドウ（APIキー対応）
    /// </summary>
    public class SheetReadWindow : EditorWindow
    {
        private ConvertSetting _selectedSetting;
        private string _searchFieldName = "humanId";
        private string _searchValue = "1";
        private bool _isProcessing = false;
        private string _lastResultMessage = "";
        private Vector2 _scrollPosition;
        
        [MenuItem("Tools/SheetSync/Read Records (API Key)")]
        public static void ShowWindow()
        {
            var window = GetWindow<SheetReadWindow>("SheetSync Read");
            window.minSize = new Vector2(500, 400);
        }
        
        private void OnGUI()
        {
            EditorGUILayout.LabelField("Sheet Read Tool", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            // API機能の説明
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("✅ この機能はAPIキーで動作します", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("スプレッドシートから特定の行を検索して読み取ることができます。", EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndVertical();
            
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
            _searchValue = EditorGUILayout.TextField(_searchValue);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            // 実行ボタン
            EditorGUI.BeginDisabledGroup(_isProcessing);
            if (GUILayout.Button("検索して読み取り", GUILayout.Height(30)))
            {
                ExecuteRead();
            }
            EditorGUI.EndDisabledGroup();
            
            // 結果表示
            if (!string.IsNullOrEmpty(_lastResultMessage))
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("結果:", EditorStyles.boldLabel);
                _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.ExpandHeight(true));
                EditorGUILayout.TextArea(_lastResultMessage, EditorStyles.textArea, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();
            }
            
            // サンプル表示
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("使用例:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("検索: humanId = 1");
            EditorGUILayout.LabelField("結果: humanId=1の行のすべてのデータが表示されます");
            EditorGUILayout.EndVertical();
        }
        
        private async void ExecuteRead()
        {
            _isProcessing = true;
            _lastResultMessage = "";
            
            try
            {
                // クエリを作成
                var query = new SimpleUpdateQuery<object>
                {
                    FieldName = _searchFieldName,
                    SearchValue = ParseValue(_searchValue)
                };
                
                // サービスを作成して実行
                SheetReadService service;
                try
                {
                    service = new SheetReadService(_selectedSetting);
                }
                catch (InvalidOperationException ex)
                {
                    _lastResultMessage = $"初期化エラー: {ex.Message}";
                    return;
                }
                
                var result = await service.SearchAndReadAsync(query);
                
                // 結果を表示
                if (result.Success)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine($"検索成功！");
                    sb.AppendLine($"見つかった行数: {result.TotalRowsFound}");
                    sb.AppendLine($"処理時間: {result.ElapsedMilliseconds}ms");
                    sb.AppendLine();
                    
                    foreach (var row in result.FoundRows)
                    {
                        sb.AppendLine($"=== Row {row.RowNumber} ===");
                        foreach (var kvp in row.Data)
                        {
                            sb.AppendLine($"  {kvp.Key}: {kvp.Value}");
                        }
                        sb.AppendLine();
                    }
                    
                    _lastResultMessage = sb.ToString();
                }
                else
                {
                    _lastResultMessage = $"読み取り失敗: {result.ErrorMessage}";
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