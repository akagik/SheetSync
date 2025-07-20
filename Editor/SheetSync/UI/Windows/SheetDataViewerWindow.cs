using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace SheetSync.UI.Windows
{
    /// <summary>
    /// スプレッドシートのデータを表示するウィンドウ
    /// </summary>
    public class SheetDataViewerWindow : EditorWindow
    {
        private ExtendedSheetData _sheetData;
        private Vector2 _scrollPosition;
        private GUIStyle _headerStyle;
        private GUIStyle _cellStyle;
        private GUIStyle _highlightStyle;
        
        // 列幅の設定
        private float _defaultColumnWidth = 150f;
        private float _rowHeight = 20f;
        private Dictionary<int, float> _columnWidths = new Dictionary<int, float>();
        
        // 検索機能
        private string _searchText = "";
        private List<(int row, int col)> _searchResults = new List<(int row, int col)>();
        private int _currentSearchIndex = -1;
        
        // 表示設定
        private bool _showRowNumbers = true;
        private int _headerRowIndex = 0;
        
        [MenuItem("Tools/SheetSync/Sheet Data Viewer")]
        public static void ShowWindow()
        {
            var window = GetWindow<SheetDataViewerWindow>("Sheet Data Viewer");
            window.minSize = new Vector2(800, 600);
        }
        
        /// <summary>
        /// SheetDataを設定して表示
        /// </summary>
        public static void ShowData(ExtendedSheetData sheetData, string title = "Sheet Data Viewer", int headerRowIndex = 0)
        {
            var window = GetWindow<SheetDataViewerWindow>(title);
            window.minSize = new Vector2(800, 600);
            window._sheetData = sheetData;
            window._headerRowIndex = headerRowIndex;
            window.InitializeStyles();
            window.Repaint();
        }
        
        private void OnEnable()
        {
            InitializeStyles();
        }
        
        private void InitializeStyles()
        {
            _headerStyle = new GUIStyle(EditorStyles.label)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { background = MakeTexture(2, 2, new Color(0.3f, 0.3f, 0.3f, 1f)) },
                fixedHeight = _rowHeight
            };
            
            _cellStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(5, 5, 0, 0),
                fixedHeight = _rowHeight
            };
            
            _highlightStyle = new GUIStyle(_cellStyle)
            {
                normal = { background = MakeTexture(2, 2, new Color(1f, 1f, 0f, 0.3f)) }
            };
        }
        
        private void OnGUI()
        {
            EditorGUILayout.BeginVertical();
            
            // ツールバー
            DrawToolbar();
            
            if (_sheetData == null)
            {
                EditorGUILayout.HelpBox("表示するデータがありません。", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }
            
            // データグリッド
            DrawDataGrid();
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            
            // 検索機能
            GUILayout.Label("検索:", EditorStyles.toolbarButton, GUILayout.Width(40));
            var newSearchText = GUILayout.TextField(_searchText, EditorStyles.toolbarTextField, GUILayout.Width(200));
            
            if (newSearchText != _searchText)
            {
                _searchText = newSearchText;
                PerformSearch();
            }
            
            // 検索結果のナビゲーション
            EditorGUI.BeginDisabledGroup(_searchResults.Count == 0);
            
            if (GUILayout.Button("◀", EditorStyles.toolbarButton, GUILayout.Width(30)))
            {
                NavigateSearch(-1);
            }
            
            GUILayout.Label($"{(_currentSearchIndex + 1)}/{_searchResults.Count}", EditorStyles.toolbarButton, GUILayout.Width(60));
            
            if (GUILayout.Button("▶", EditorStyles.toolbarButton, GUILayout.Width(30)))
            {
                NavigateSearch(1);
            }
            
            EditorGUI.EndDisabledGroup();
            
            GUILayout.FlexibleSpace();
            
            // 表示設定
            _showRowNumbers = GUILayout.Toggle(_showRowNumbers, "行番号表示", EditorStyles.toolbarButton);
            
            // 情報表示
            GUILayout.Label($"行: {_sheetData.RowCount}, 列: {_sheetData.ColumnCount}", EditorStyles.toolbarButton);
            
            if (_headerRowIndex > 0)
            {
                GUILayout.Label($"ヘッダー行: {_headerRowIndex + 1}", EditorStyles.toolbarButton);
            }
            
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawDataGrid()
        {
            var viewRect = EditorGUILayout.GetControlRect(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            
            // 全体のコンテンツサイズを計算
            float totalWidth = _showRowNumbers ? 50 : 0;
            for (int col = 0; col < _sheetData.ColumnCount; col++)
            {
                totalWidth += GetColumnWidth(col);
            }
            float totalHeight = _sheetData.RowCount * _rowHeight;
            
            var contentRect = new Rect(0, 0, totalWidth, totalHeight);
            _scrollPosition = GUI.BeginScrollView(viewRect, _scrollPosition, contentRect);
            
            // ヘッダー行の描画
            float xPos = 0;
            
            if (_showRowNumbers)
            {
                GUI.Label(new Rect(xPos, 0, 50, _rowHeight), "", _headerStyle);
                xPos += 50;
            }
            
            for (int col = 0; col < _sheetData.ColumnCount; col++)
            {
                var columnWidth = GetColumnWidth(col);
                var headerRect = new Rect(xPos, 0, columnWidth, _rowHeight);
                
                // ヘッダーテキスト
                string headerText = "";
                if (_sheetData.EditedValues.Count > 0 && _sheetData.EditedValues[0] != null && col < _sheetData.EditedValues[0].Count)
                {
                    headerText = _sheetData.EditedValues[0][col]?.ToString() ?? "";
                }
                
                GUI.Label(headerRect, headerText, _headerStyle);
                
                // 列幅のリサイズハンドル
                var resizeRect = new Rect(xPos + columnWidth - 5, 0, 10, _rowHeight);
                EditorGUIUtility.AddCursorRect(resizeRect, MouseCursor.ResizeHorizontal);
                
                if (Event.current.type == EventType.MouseDown && resizeRect.Contains(Event.current.mousePosition))
                {
                    // リサイズ開始（簡易実装）
                }
                
                xPos += columnWidth;
            }
            
            // データ行の描画
            for (int row = 1; row < _sheetData.EditedValues.Count; row++)
            {
                xPos = 0;
                float yPos = row * _rowHeight;
                
                // 行番号
                if (_showRowNumbers)
                {
                    GUI.Label(new Rect(xPos, yPos, 50, _rowHeight), (row + 1).ToString(), _headerStyle);
                    xPos += 50;
                }
                
                // セルデータ
                var rowData = _sheetData.EditedValues[row];
                if (rowData != null)
                {
                    for (int col = 0; col < _sheetData.ColumnCount && col < rowData.Count; col++)
                    {
                        var columnWidth = GetColumnWidth(col);
                        var cellRect = new Rect(xPos, yPos, columnWidth, _rowHeight);
                        
                        var cellValue = rowData[col]?.ToString() ?? "";
                        
                        // 検索結果のハイライト
                        var style = _cellStyle;
                        if (IsSearchMatch(row, col))
                        {
                            style = _highlightStyle;
                        }
                        
                        GUI.Label(cellRect, cellValue, style);
                        
                        // セルの境界線
                        DrawBorder(cellRect, new Color(0.5f, 0.5f, 0.5f, 0.3f));
                        
                        xPos += columnWidth;
                    }
                }
            }
            
            GUI.EndScrollView();
        }
        
        private float GetColumnWidth(int columnIndex)
        {
            if (_columnWidths.TryGetValue(columnIndex, out float width))
            {
                return width;
            }
            
            // 自動幅計算（簡易版）
            float maxWidth = _defaultColumnWidth;
            
            // ヘッダーの幅を考慮
            if (_sheetData.EditedValues.Count > 0 && _sheetData.EditedValues[0] != null && columnIndex < _sheetData.EditedValues[0].Count)
            {
                var headerText = _sheetData.EditedValues[0][columnIndex]?.ToString() ?? "";
                var headerWidth = GUI.skin.label.CalcSize(new GUIContent(headerText)).x + 20;
                maxWidth = Mathf.Max(maxWidth, headerWidth);
            }
            
            return maxWidth;
        }
        
        private void PerformSearch()
        {
            _searchResults.Clear();
            _currentSearchIndex = -1;
            
            if (string.IsNullOrEmpty(_searchText))
                return;
            
            var searchLower = _searchText.ToLower();
            
            for (int row = 0; row < _sheetData.EditedValues.Count; row++)
            {
                var rowData = _sheetData.EditedValues[row];
                if (rowData == null) continue;
                
                for (int col = 0; col < rowData.Count; col++)
                {
                    var cellValue = rowData[col]?.ToString() ?? "";
                    if (cellValue.ToLower().Contains(searchLower))
                    {
                        _searchResults.Add((row, col));
                    }
                }
            }
            
            if (_searchResults.Count > 0)
            {
                _currentSearchIndex = 0;
                ScrollToCell(_searchResults[0].row, _searchResults[0].col);
            }
        }
        
        private void NavigateSearch(int direction)
        {
            if (_searchResults.Count == 0) return;
            
            _currentSearchIndex = (_currentSearchIndex + direction + _searchResults.Count) % _searchResults.Count;
            var (row, col) = _searchResults[_currentSearchIndex];
            ScrollToCell(row, col);
        }
        
        private void ScrollToCell(int row, int col)
        {
            // セルが見えるようにスクロール位置を調整
            float xPos = _showRowNumbers ? 50 : 0;
            for (int i = 0; i < col; i++)
            {
                xPos += GetColumnWidth(i);
            }
            
            float yPos = row * _rowHeight;
            
            var viewRect = position;
            viewRect.height -= 40; // ツールバーの高さ
            
            // スクロール位置の調整
            if (xPos < _scrollPosition.x)
            {
                _scrollPosition.x = xPos;
            }
            else if (xPos + GetColumnWidth(col) > _scrollPosition.x + viewRect.width)
            {
                _scrollPosition.x = xPos + GetColumnWidth(col) - viewRect.width;
            }
            
            if (yPos < _scrollPosition.y)
            {
                _scrollPosition.y = yPos;
            }
            else if (yPos + _rowHeight > _scrollPosition.y + viewRect.height)
            {
                _scrollPosition.y = yPos + _rowHeight - viewRect.height;
            }
            
            Repaint();
        }
        
        private bool IsSearchMatch(int row, int col)
        {
            if (_searchResults.Count == 0) return false;
            
            return _searchResults.Contains((row, col));
        }
        
        private void DrawBorder(Rect rect, Color color)
        {
            var oldColor = GUI.color;
            GUI.color = color;
            
            // 右境界
            GUI.DrawTexture(new Rect(rect.xMax - 1, rect.y, 1, rect.height), EditorGUIUtility.whiteTexture);
            // 下境界
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - 1, rect.width, 1), EditorGUIUtility.whiteTexture);
            
            GUI.color = oldColor;
        }
        
        private Texture2D MakeTexture(int width, int height, Color color)
        {
            var pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }
            
            var texture = new Texture2D(width, height);
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
    }
}