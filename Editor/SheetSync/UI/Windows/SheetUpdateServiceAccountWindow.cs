using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using SheetSync.Services.Auth;
using SheetSync.Services.Update;
using SheetSync.Services;
using SheetSync.Services.Common;
using KoheiUtils;

namespace SheetSync.UI.Windows
{
    /// <summary>
    /// サービスアカウント認証を使用したスプレッドシート更新ウィンドウ
    /// </summary>
    public class SheetUpdateServiceAccountWindow : EditorWindow
    {
        #region フィールド
        
        private SheetUpdateServiceAccountService _updateService;
        private ConvertSetting _selectedSetting;
        private ExtendedSheetData _currentSheetData;
        private ExtendedSheetData _previewSheetData;
        
        // 検索条件
        private string _keyColumn = "humanId";
        private string _keyValue = "1";
        
        // 更新データ
        private readonly List<UpdateField> _updateFields = new List<UpdateField>();
        
        // UI状態
        private bool _isProcessing = false;
        private bool _showPreview = false;
        private string _statusMessage = "";
        private MessageType _statusType = MessageType.Info;
        
        // スクロール位置
        private Vector2 _scrollPosition;
        
        #endregion
        
        #region 内部クラス
        
        [Serializable]
        private class UpdateField
        {
            public string columnName = "";
            public string newValue = "";
            public bool isEnabled = true;
        }
        
        #endregion
        
        #region Unity エディターイベント
        
        [MenuItem("Tools/SheetSync/Update Records (Service Account)")]
        public static void ShowWindow()
        {
            var window = GetWindow<SheetUpdateServiceAccountWindow>("Sheet Update (Service Account)");
            window.minSize = new Vector2(600, 500);
            window.Show();
        }
        
        private void OnEnable()
        {
            _updateService = new SheetUpdateServiceAccountService();
            
            // デフォルトで1つの更新フィールドを追加
            if (_updateFields.Count == 0)
            {
                _updateFields.Add(new UpdateField { columnName = "name", newValue = "Tanaka" });
            }
        }
        
        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            
            DrawHeader();
            DrawAuthenticationSection();
            
            if (GoogleServiceAccountAuth.IsAuthenticated)
            {
                DrawSettingSection();
                
                if (_selectedSetting != null)
                {
                    DrawSearchSection();
                    DrawUpdateFieldsSection();
                    DrawActionButtons();
                }
            }
            
            DrawStatusSection();
            
            EditorGUILayout.EndScrollView();
        }
        
        #endregion
        
        #region UI描画メソッド
        
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
                DrawAuthenticatedStatus();
            }
            else
            {
                DrawAuthenticationPrompt();
            }
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10);
        }
        
        private void DrawAuthenticatedStatus()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("✓ サービスアカウント認証済み", EditorStyles.miniLabel);
            if (GUILayout.Button("認証をクリア", GUILayout.Width(100)))
            {
                GoogleServiceAccountAuth.ClearAuthentication();
                ClearData();
                Repaint();
            }
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawAuthenticationPrompt()
        {
            EditorGUILayout.LabelField("認証が必要です", EditorStyles.miniLabel);
            
            EditorGUI.BeginDisabledGroup(_isProcessing);
            if (GUILayout.Button("認証を開始", GUILayout.Height(30)))
            {
                StartServiceAccountAuth();
            }
            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox(
                "サービスアカウントキー（JSON）が必要です。\n" +
                "ProjectSettings/SheetSync/service-account-key.json に配置してください。",
                MessageType.Info);
        }
        
        private void DrawSettingSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("スプレッドシート設定", EditorStyles.boldLabel);
            
            EditorGUI.BeginChangeCheck();
            _selectedSetting = (ConvertSetting)EditorGUILayout.ObjectField(
                "Convert Setting", 
                _selectedSetting, 
                typeof(ConvertSetting), 
                false);
            
            if (EditorGUI.EndChangeCheck())
            {
                // 設定が変更されたらデータをクリア
                ClearData();
            }
            
            if (_selectedSetting == null)
            {
                EditorGUILayout.HelpBox("ConvertSettingを選択してください", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.Space(5);
                
                EditorGUI.BeginDisabledGroup(_isProcessing);
                if (GUILayout.Button("スプレッドシートを読み込む", GUILayout.Height(25)))
                {
                    LoadSpreadsheetData();
                }
                EditorGUI.EndDisabledGroup();
            }
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10);
        }
        
        private void DrawSearchSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("検索条件", EditorStyles.boldLabel);
            
            _keyColumn = EditorGUILayout.TextField("キー列名", _keyColumn);
            _keyValue = EditorGUILayout.TextField("検索値", _keyValue);
            
            if (_currentSheetData != null)
            {
                var columnIndex = _currentSheetData.GetColumnIndex(_keyColumn);
                if (columnIndex < 0)
                {
                    EditorGUILayout.HelpBox($"列 '{_keyColumn}' が見つかりません", MessageType.Warning);
                }
                else
                {
                    var rowIndex = _currentSheetData.GetRowIndex(_keyColumn, _keyValue);
                    if (rowIndex < 0)
                    {
                        EditorGUILayout.HelpBox($"{_keyColumn}='{_keyValue}' の行が見つかりません", MessageType.Warning);
                    }
                    else
                    {
                        EditorGUILayout.HelpBox($"行 {rowIndex + 1} が更新対象です", MessageType.Info);
                    }
                }
            }
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10);
        }
        
        private void DrawUpdateFieldsSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("更新内容", EditorStyles.boldLabel);
            
            if (GUILayout.Button("+", GUILayout.Width(20)))
            {
                _updateFields.Add(new UpdateField());
            }
            EditorGUILayout.EndHorizontal();
            
            for (int i = 0; i < _updateFields.Count; i++)
            {
                DrawUpdateField(i);
            }
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(10);
        }
        
        private void DrawUpdateField(int index)
        {
            var field = _updateFields[index];
            
            EditorGUILayout.BeginHorizontal();
            
            field.isEnabled = EditorGUILayout.Toggle(field.isEnabled, GUILayout.Width(20));
            
            EditorGUI.BeginDisabledGroup(!field.isEnabled);
            field.columnName = EditorGUILayout.TextField(field.columnName, GUILayout.Width(150));
            EditorGUILayout.LabelField("→", GUILayout.Width(20));
            field.newValue = EditorGUILayout.TextField(field.newValue);
            EditorGUI.EndDisabledGroup();
            
            if (GUILayout.Button("-", GUILayout.Width(20)))
            {
                _updateFields.RemoveAt(index);
            }
            
            EditorGUILayout.EndHorizontal();
            
            // 列名の検証
            if (field.isEnabled && _currentSheetData != null)
            {
                var columnIndex = _currentSheetData.GetColumnIndex(field.columnName);
                if (columnIndex < 0 && !string.IsNullOrEmpty(field.columnName))
                {
                    EditorGUILayout.HelpBox($"列 '{field.columnName}' が見つかりません", MessageType.Warning);
                }
            }
        }
        
        private void DrawActionButtons()
        {
            EditorGUILayout.BeginHorizontal();
            
            // プレビューボタン
            EditorGUI.BeginDisabledGroup(_isProcessing || !HasValidInput());
            if (GUILayout.Button("変更をプレビュー", GUILayout.Height(30)))
            {
                PreviewChanges();
            }
            EditorGUI.EndDisabledGroup();
            
            // 更新ボタン（プレビュー済みの場合のみ）
            EditorGUI.BeginDisabledGroup(_isProcessing || !_showPreview);
            if (GUILayout.Button("更新を実行", GUILayout.Height(30)))
            {
                ExecuteUpdate();
            }
            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.EndHorizontal();
            
            // サービスアカウント特有の注意事項
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox(
                "重要: スプレッドシートをサービスアカウントのメールアドレスと共有する必要があります。\n" +
                "サービスアカウントのメールアドレスは service-account-key.json 内の client_email です。",
                MessageType.Warning);
        }
        
        private void DrawStatusSection()
        {
            if (!string.IsNullOrEmpty(_statusMessage))
            {
                EditorGUILayout.Space(10);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.HelpBox(_statusMessage, _statusType);
                EditorGUILayout.EndVertical();
            }
        }
        
        #endregion
        
        #region ビジネスロジック
        
        private async void StartServiceAccountAuth()
        {
            try
            {
                _isProcessing = true;
                SetStatus("サービスアカウント認証中...", MessageType.Info);
                Repaint();
                
                var success = await GoogleServiceAccountAuth.AuthorizeAsync();
                
                if (success)
                {
                    SetStatus("サービスアカウント認証に成功しました！", MessageType.Info);
                }
                else
                {
                    SetStatus("サービスアカウント認証に失敗しました。設定を確認してください。", MessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SetStatus($"認証エラー: {ex.Message}", MessageType.Error);
                Debug.LogException(ex);
            }
            finally
            {
                _isProcessing = false;
                Repaint();
            }
        }
        
        private async void LoadSpreadsheetData()
        {
            try
            {
                _isProcessing = true;
                SetStatus("スプレッドシートを読み込み中...", MessageType.Info);
                Repaint();
                
                var service = GoogleServiceAccountAuth.GetAuthenticatedService();
                var sheetName = await GoogleSheetsUtility.GetSheetNameFromSettingAsync(service, _selectedSetting);
                
                if (string.IsNullOrEmpty(sheetName))
                {
                    SetStatus("シート名の取得に失敗しました。", MessageType.Error);
                    return;
                }
                
                _currentSheetData = await SheetDataIntegrationExample.LoadSheetDataAsync(
                    _selectedSetting.sheetID,
                    sheetName,
                    true
                );
                
                if (_currentSheetData != null)
                {
                    // キーインデックスを構築
                    if (!string.IsNullOrEmpty(_keyColumn))
                    {
                        try
                        {
                            _currentSheetData.BuildKeyIndex(_keyColumn);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"キーインデックスの構築に失敗: {ex.Message}");
                        }
                    }
                    
                    SetStatus($"データを読み込みました: {_currentSheetData.RowCount}行 x {_currentSheetData.ColumnCount}列", MessageType.Info);
                    _showPreview = false;
                }
                else
                {
                    SetStatus("データの読み込みに失敗しました。", MessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SetStatus($"読み込みエラー: {ex.Message}", MessageType.Error);
                Debug.LogException(ex);
            }
            finally
            {
                _isProcessing = false;
                Repaint();
            }
        }
        
        private void PreviewChanges()
        {
            if (_currentSheetData == null || !HasValidInput())
                return;
            
            try
            {
                // プレビュー用のSheetDataを作成（コピー）
                _previewSheetData = new ExtendedSheetData(_currentSheetData.EditedValues);
                _previewSheetData.BuildKeyIndex(_keyColumn);
                
                // 更新データを構築
                var updateData = new Dictionary<string, object>();
                foreach (var field in _updateFields)
                {
                    if (field.isEnabled && !string.IsNullOrEmpty(field.columnName))
                    {
                        updateData[field.columnName] = field.newValue;
                    }
                }
                
                // プレビューデータに変更を適用
                try
                {
                    _previewSheetData.UpdateRowByKey(_keyColumn, _keyValue, updateData);
                    
                    // 差分ウィンドウを表示
                    SheetDataDiffWindow.ShowDiff(_previewSheetData);
                    
                    _showPreview = true;
                    SetStatus("変更内容を確認してください。問題なければ「更新を実行」を押してください。", MessageType.Info);
                }
                catch (ArgumentException ex)
                {
                    SetStatus($"プレビューエラー: {ex.Message}", MessageType.Error);
                    _showPreview = false;
                }
            }
            catch (Exception ex)
            {
                SetStatus($"プレビューエラー: {ex.Message}", MessageType.Error);
                Debug.LogException(ex);
                _showPreview = false;
            }
        }
        
        private async void ExecuteUpdate()
        {
            if (_updateService == null || _selectedSetting == null || !_showPreview)
                return;
            
            _isProcessing = true;
            SetStatus("更新中...", MessageType.Info);
            Repaint();
            
            try
            {
                // 更新データを構築
                var updateData = new Dictionary<string, object>();
                foreach (var field in _updateFields)
                {
                    if (field.isEnabled && !string.IsNullOrEmpty(field.columnName))
                    {
                        updateData[field.columnName] = field.newValue;
                    }
                }
                
                // シート名を取得
                var service = GoogleServiceAccountAuth.GetAuthenticatedService();
                var sheetName = await GoogleSheetsUtility.GetSheetNameFromSettingAsync(service, _selectedSetting);
                
                if (string.IsNullOrEmpty(sheetName))
                {
                    SetStatus("シート名の取得に失敗しました。", MessageType.Error);
                    return;
                }
                
                // 更新を実行
                var success = await _updateService.UpdateRowAsync(
                    _selectedSetting.sheetID,
                    sheetName,
                    _keyColumn,
                    _keyValue,
                    updateData
                );
                
                if (success)
                {
                    var updatedFields = string.Join(", ", updateData.Keys);
                    SetStatus($"更新成功: {_keyColumn}='{_keyValue}' の {updatedFields} を更新しました。", MessageType.Info);
                    
                    // データを再読み込み
                    LoadSpreadsheetData();
                }
                else
                {
                    SetStatus("更新に失敗しました。ログを確認してください。", MessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SetStatus($"更新エラー: {ex.Message}", MessageType.Error);
                Debug.LogException(ex);
            }
            finally
            {
                _isProcessing = false;
                _showPreview = false;
                Repaint();
            }
        }
        
        #endregion
        
        #region ヘルパーメソッド
        
        private bool HasValidInput()
        {
            if (string.IsNullOrEmpty(_keyColumn) || string.IsNullOrEmpty(_keyValue))
                return false;
            
            // 有効な更新フィールドが少なくとも1つあるか確認
            foreach (var field in _updateFields)
            {
                if (field.isEnabled && !string.IsNullOrEmpty(field.columnName))
                {
                    return true;
                }
            }
            
            return false;
        }
        
        private void SetStatus(string message, MessageType type)
        {
            _statusMessage = message;
            _statusType = type;
        }
        
        private void ClearData()
        {
            _currentSheetData = null;
            _previewSheetData = null;
            _showPreview = false;
            _statusMessage = "";
        }
        
        #endregion
    }
}