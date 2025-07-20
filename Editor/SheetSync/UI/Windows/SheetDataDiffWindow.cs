using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace SheetSync.UI.Windows
{
    /// <summary>
    /// ExtendedSheetDataの変更差分を表示するウィンドウ
    /// </summary>
    public class SheetDataDiffWindow : EditorWindow
    {
        private ExtendedSheetData _sheetData;
        private Vector2 _scrollPosition;
        private bool _showOnlyChanges = true;
        
        // 色の定義
        private static readonly Color AddedColor = new Color(0.2f, 0.8f, 0.2f, 0.3f);
        private static readonly Color DeletedColor = new Color(0.8f, 0.2f, 0.2f, 0.3f);
        private static readonly Color ModifiedColor = new Color(0.8f, 0.8f, 0.2f, 0.3f);
        
        [MenuItem("Tools/SheetSync/Sheet Data Diff Viewer")]
        public static void ShowWindow()
        {
            var window = GetWindow<SheetDataDiffWindow>("Sheet Data Diff");
            window.minSize = new Vector2(600, 400);
        }
        
        /// <summary>
        /// ExtendedSheetDataを設定して差分を表示
        /// </summary>
        public static void ShowDiff(ExtendedSheetData sheetData)
        {
            var window = GetWindow<SheetDataDiffWindow>("Sheet Data Diff");
            window.minSize = new Vector2(600, 400);
            window._sheetData = sheetData;
            window.Repaint();
        }
        
        private void OnGUI()
        {
            EditorGUILayout.BeginVertical();
            
            // ヘッダー
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("Sheet Data 変更差分ビューアー", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            _showOnlyChanges = GUILayout.Toggle(_showOnlyChanges, "変更のみ表示", EditorStyles.toolbarButton);
            EditorGUILayout.EndHorizontal();
            
            if (_sheetData == null)
            {
                EditorGUILayout.HelpBox("差分を表示するSheetDataがありません。", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }
            
            // 変更サマリー
            DrawChangeSummary();
            
            // 差分表示
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            
            if (_showOnlyChanges)
            {
                DrawChangesOnly();
            }
            else
            {
                DrawFullDataWithHighlight();
            }
            
            EditorGUILayout.EndScrollView();
            
            // ボタン
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("変更をクリア", GUILayout.Width(100)))
            {
                if (EditorUtility.DisplayDialog("確認", "すべての変更履歴をクリアしますか？", "はい", "いいえ"))
                {
                    _sheetData.ClearChanges();
                    Repaint();
                }
            }
            
            if (GUILayout.Button("編集をリセット", GUILayout.Width(100)))
            {
                if (EditorUtility.DisplayDialog("確認", "すべての編集を元に戻しますか？", "はい", "いいえ"))
                {
                    _sheetData.ResetEdits();
                    Repaint();
                }
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }
        
        /// <summary>
        /// 変更サマリーを描画
        /// </summary>
        private void DrawChangeSummary()
        {
            if (!_sheetData.HasChanges)
            {
                EditorGUILayout.HelpBox("変更はありません。", MessageType.Info);
                return;
            }
            
            int updateCount = 0, insertCount = 0, deleteCount = 0;
            foreach (var change in _sheetData.Changes)
            {
                switch (change.Type)
                {
                    case ExtendedSheetData.ChangeType.Update:
                        updateCount++;
                        break;
                    case ExtendedSheetData.ChangeType.Insert:
                        insertCount++;
                        break;
                    case ExtendedSheetData.ChangeType.Delete:
                        deleteCount++;
                        break;
                }
            }
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label($"変更サマリー: ", EditorStyles.boldLabel);
            
            GUI.color = ModifiedColor;
            GUILayout.Label($"更新: {updateCount}");
            
            GUI.color = AddedColor;
            GUILayout.Label($"挿入: {insertCount}");
            
            GUI.color = DeletedColor;
            GUILayout.Label($"削除: {deleteCount}");
            
            GUI.color = Color.white;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
        }
        
        /// <summary>
        /// 変更のみを表示
        /// </summary>
        private void DrawChangesOnly()
        {
            // 変更を行番号でグループ化
            var changesByRow = new Dictionary<int, List<ExtendedSheetData.ChangeRecord>>();
            foreach (var change in _sheetData.Changes)
            {
                if (!changesByRow.ContainsKey(change.RowIndex))
                {
                    changesByRow[change.RowIndex] = new List<ExtendedSheetData.ChangeRecord>();
                }
                changesByRow[change.RowIndex].Add(change);
            }
            
            // 行番号順に表示
            var sortedRows = new List<int>(changesByRow.Keys);
            sortedRows.Sort();
            
            foreach (var rowIndex in sortedRows)
            {
                var changes = changesByRow[rowIndex];
                var firstChange = changes[0];
                
                // 行の背景色を設定
                Color bgColor = Color.white;
                switch (firstChange.Type)
                {
                    case ExtendedSheetData.ChangeType.Update:
                        bgColor = ModifiedColor;
                        break;
                    case ExtendedSheetData.ChangeType.Insert:
                        bgColor = AddedColor;
                        break;
                    case ExtendedSheetData.ChangeType.Delete:
                        bgColor = DeletedColor;
                        break;
                }
                
                GUI.backgroundColor = bgColor;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUI.backgroundColor = Color.white;
                
                // 行番号とタイプを表示
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label($"行 {rowIndex + 1}", EditorStyles.boldLabel, GUILayout.Width(60));
                GUILayout.Label($"[{GetChangeTypeLabel(firstChange.Type)}]", GUILayout.Width(60));
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
                
                // 変更内容を表示
                if (firstChange.Type == ExtendedSheetData.ChangeType.Update)
                {
                    foreach (var change in changes)
                    {
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Space(20);
                        GUILayout.Label($"{change.ColumnName}:", GUILayout.Width(100));
                        GUILayout.Label($"{change.OldValue ?? "(null)"}", GUILayout.Width(150));
                        GUILayout.Label("→", GUILayout.Width(20));
                        GUI.color = AddedColor;
                        GUILayout.Label($"{change.NewValue ?? "(null)"}", GUILayout.Width(150));
                        GUI.color = Color.white;
                        EditorGUILayout.EndHorizontal();
                    }
                }
                else if (firstChange.Type == ExtendedSheetData.ChangeType.Insert)
                {
                    if (firstChange.RowData != null)
                    {
                        foreach (var kvp in firstChange.RowData)
                        {
                            EditorGUILayout.BeginHorizontal();
                            GUILayout.Space(20);
                            GUILayout.Label($"{kvp.Key}:", GUILayout.Width(100));
                            GUI.color = AddedColor;
                            GUILayout.Label($"{kvp.Value ?? "(null)"}");
                            GUI.color = Color.white;
                            EditorGUILayout.EndHorizontal();
                        }
                    }
                }
                else if (firstChange.Type == ExtendedSheetData.ChangeType.Delete)
                {
                    if (firstChange.RowData != null)
                    {
                        foreach (var kvp in firstChange.RowData)
                        {
                            EditorGUILayout.BeginHorizontal();
                            GUILayout.Space(20);
                            GUILayout.Label($"{kvp.Key}:", GUILayout.Width(100));
                            GUI.color = DeletedColor;
                            GUILayout.Label($"{kvp.Value ?? "(null)"}");
                            GUI.color = Color.white;
                            EditorGUILayout.EndHorizontal();
                        }
                    }
                }
                
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
            }
        }
        
        /// <summary>
        /// 全データをハイライト付きで表示
        /// </summary>
        private void DrawFullDataWithHighlight()
        {
            var editedValues = _sheetData.EditedValues;
            if (editedValues == null || editedValues.Count == 0) return;
            
            // ヘッダー行を表示
            if (editedValues.Count > 0 && editedValues[0] != null)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("行", EditorStyles.boldLabel, GUILayout.Width(40));
                
                foreach (var header in editedValues[0])
                {
                    GUILayout.Label(header?.ToString() ?? "", EditorStyles.boldLabel, GUILayout.Width(100));
                }
                EditorGUILayout.EndHorizontal();
            }
            
            // データ行を表示
            for (int i = 1; i < editedValues.Count; i++)
            {
                var row = editedValues[i];
                if (row == null) continue;
                
                // 行の変更状態を確認
                var rowChanges = GetRowChanges(i);
                Color rowColor = GetRowColor(rowChanges);
                
                GUI.backgroundColor = rowColor;
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                GUI.backgroundColor = Color.white;
                
                GUILayout.Label((i + 1).ToString(), GUILayout.Width(40));
                
                for (int j = 0; j < row.Count; j++)
                {
                    var cellChange = GetCellChange(rowChanges, j);
                    if (cellChange != null && cellChange.Type == ExtendedSheetData.ChangeType.Update)
                    {
                        GUI.color = ModifiedColor;
                    }
                    
                    GUILayout.Label(row[j]?.ToString() ?? "", GUILayout.Width(100));
                    GUI.color = Color.white;
                }
                
                EditorGUILayout.EndHorizontal();
            }
        }
        
        /// <summary>
        /// 行の変更を取得
        /// </summary>
        private List<ExtendedSheetData.ChangeRecord> GetRowChanges(int rowIndex)
        {
            var changes = new List<ExtendedSheetData.ChangeRecord>();
            foreach (var change in _sheetData.Changes)
            {
                if (change.RowIndex == rowIndex)
                {
                    changes.Add(change);
                }
            }
            return changes;
        }
        
        /// <summary>
        /// セルの変更を取得
        /// </summary>
        private ExtendedSheetData.ChangeRecord GetCellChange(List<ExtendedSheetData.ChangeRecord> rowChanges, int columnIndex)
        {
            var columnName = GetColumnName(columnIndex);
            foreach (var change in rowChanges)
            {
                if (change.ColumnName == columnName)
                {
                    return change;
                }
            }
            return null;
        }
        
        /// <summary>
        /// 列名を取得
        /// </summary>
        private string GetColumnName(int columnIndex)
        {
            if (_sheetData.EditedValues.Count > 0 && _sheetData.EditedValues[0] != null)
            {
                var headers = _sheetData.EditedValues[0];
                if (columnIndex < headers.Count)
                {
                    return headers[columnIndex]?.ToString() ?? $"Column{columnIndex}";
                }
            }
            return $"Column{columnIndex}";
        }
        
        /// <summary>
        /// 行の色を取得
        /// </summary>
        private Color GetRowColor(List<ExtendedSheetData.ChangeRecord> changes)
        {
            if (changes.Count == 0) return Color.white;
            
            var firstChange = changes[0];
            switch (firstChange.Type)
            {
                case ExtendedSheetData.ChangeType.Update:
                    return ModifiedColor;
                case ExtendedSheetData.ChangeType.Insert:
                    return AddedColor;
                case ExtendedSheetData.ChangeType.Delete:
                    return DeletedColor;
                default:
                    return Color.white;
            }
        }
        
        /// <summary>
        /// 変更タイプのラベルを取得
        /// </summary>
        private string GetChangeTypeLabel(ExtendedSheetData.ChangeType type)
        {
            switch (type)
            {
                case ExtendedSheetData.ChangeType.Update:
                    return "更新";
                case ExtendedSheetData.ChangeType.Insert:
                    return "挿入";
                case ExtendedSheetData.ChangeType.Delete:
                    return "削除";
                default:
                    return type.ToString();
            }
        }
    }
}