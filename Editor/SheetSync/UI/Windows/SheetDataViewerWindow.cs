using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Text;
using System.Linq;

namespace SheetSync.UI.Windows
{
    /// <summary>
    /// スプレッドシートのデータを表示するウィンドウ（最適化版）
    /// </summary>
    public class SheetDataViewerWindow : EditorWindow
    {
        private ExtendedSheetData _sheetData;
        private Vector2 _scrollPosition;
        private GUIStyle _headerStyle;
        private GUIStyle _cellStyle;
        private GUIStyle _selectedCellStyle;
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
        
        // 仮想スクロール用
        private int _visibleRowStart = 0;
        private int _visibleRowEnd = 0;
        private int _visibleColStart = 0;
        private int _visibleColEnd = 0;
        
        // セル選択
        private bool _isSelecting = false;
        private Vector2Int _selectionStart;
        private Vector2Int _selectionEnd;
        private HashSet<(int row, int col)> _selectedCells = new HashSet<(int row, int col)>();
        
        // キャッシュ
        private float[] _columnStartPositions;
        private float _totalWidth;
        private bool _needsRecalculation = true;
        
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
            window._needsRecalculation = true;
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
                fixedHeight = _rowHeight,
                clipping = TextClipping.Clip
            };
            
            _selectedCellStyle = new GUIStyle(_cellStyle)
            {
                normal = { background = MakeTexture(2, 2, new Color(0.2f, 0.5f, 0.8f, 0.3f)) }
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
            DrawOptimizedDataGrid();
            
            // キーボードイベント処理
            HandleKeyboardEvents();
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawToolbar()
        {
            // エラーを修正: 未使用のコードを削除
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
            
            // コピーボタン
            EditorGUI.BeginDisabledGroup(_selectedCells.Count == 0);
            if (GUILayout.Button("コピー", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                CopySelectedCells();
            }
            EditorGUI.EndDisabledGroup();
            
            // 表示設定
            _showRowNumbers = GUILayout.Toggle(_showRowNumbers, "行番号", EditorStyles.toolbarButton);
            
            // 情報表示
            GUILayout.Label($"行: {_sheetData.RowCount}, 列: {_sheetData.ColumnCount}", EditorStyles.toolbarButton);
            
            if (_headerRowIndex > 0)
            {
                GUILayout.Label($"ヘッダー行: {_headerRowIndex + 1}", EditorStyles.toolbarButton);
            }
            
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawOptimizedDataGrid()
        {
            var viewRect = EditorGUILayout.GetControlRect(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            
            // 列の開始位置を事前計算
            if (_needsRecalculation)
            {
                CalculateColumnPositions();
                _needsRecalculation = false;
            }
            
            float totalHeight = _sheetData.RowCount * _rowHeight;
            
            var contentRect = new Rect(0, 0, _totalWidth, totalHeight);
            
            // スクロール処理
            var oldScrollPosition = _scrollPosition;
            _scrollPosition = GUI.BeginScrollView(viewRect, _scrollPosition, contentRect);
            
            // 可視範囲の計算（仮想スクロール）
            CalculateVisibleRange(viewRect);
            
            // マウスイベント処理
            HandleMouseEvents(viewRect);
            
            // ヘッダー行の描画（スクロール位置に固定）
            DrawHeaders(viewRect);
            
            // 可視範囲のデータのみ描画
            DrawVisibleCells(viewRect);
            
            GUI.EndScrollView();
            
            // スクロール位置が変更された場合は再描画
            if (oldScrollPosition != _scrollPosition)
            {
                Repaint();
            }
        }
        
        private void CalculateColumnPositions()
        {
            _columnStartPositions = new float[_sheetData.ColumnCount + 1];
            _totalWidth = _showRowNumbers ? 50 : 0;
            _columnStartPositions[0] = _totalWidth;
            
            for (int col = 0; col < _sheetData.ColumnCount; col++)
            {
                var width = GetColumnWidth(col);
                _totalWidth += width;
                _columnStartPositions[col + 1] = _totalWidth;
            }
        }
        
        private void CalculateVisibleRange(Rect viewRect)
        {
            // 可視行の計算
            _visibleRowStart = Mathf.Max(0, (int)(_scrollPosition.y / _rowHeight) - 1);
            _visibleRowEnd = Mathf.Min(_sheetData.RowCount, (int)((_scrollPosition.y + viewRect.height) / _rowHeight) + 2);
            
            // 可視列の計算
            _visibleColStart = 0;
            _visibleColEnd = _sheetData.ColumnCount;
            
            float xStart = _scrollPosition.x;
            float xEnd = _scrollPosition.x + viewRect.width;
            
            for (int col = 0; col < _sheetData.ColumnCount; col++)
            {
                if (_columnStartPositions[col + 1] > xStart && _visibleColStart == 0)
                {
                    _visibleColStart = Mathf.Max(0, col - 1);
                }
                if (_columnStartPositions[col] > xEnd && _visibleColEnd == _sheetData.ColumnCount)
                {
                    _visibleColEnd = Mathf.Min(_sheetData.ColumnCount, col + 1);
                    break;
                }
            }
        }
        
        private void DrawHeaders(Rect viewRect)
        {
            // ヘッダー行を固定位置に描画
            float yPos = _scrollPosition.y;
            
            // 行番号ヘッダー
            if (_showRowNumbers)
            {
                var headerRect = new Rect(0, yPos, 50, _rowHeight);
                GUI.Label(headerRect, "", _headerStyle);
            }
            
            // 列ヘッダー（可視範囲のみ）
            for (int col = _visibleColStart; col < _visibleColEnd && col < _sheetData.ColumnCount; col++)
            {
                var columnWidth = GetColumnWidth(col);
                var xPosition = _columnStartPositions[col] - _scrollPosition.x;
                
                if (xPosition + columnWidth < 0) continue;
                if (xPosition > viewRect.width) break;
                
                var headerRect = new Rect(xPosition, yPos, columnWidth, _rowHeight);
                
                string headerText = "";
                if (_headerRowIndex >= 0 && _headerRowIndex < _sheetData.EditedValues.Count && 
                    _sheetData.EditedValues[_headerRowIndex] != null && 
                    col < _sheetData.EditedValues[_headerRowIndex].Count)
                {
                    headerText = _sheetData.EditedValues[_headerRowIndex][col]?.ToString() ?? "";
                }
                
                GUI.Label(headerRect, headerText, _headerStyle);
            }
        }
        
        private void DrawVisibleCells(Rect viewRect)
        {
            // ヘッダー行の高さを考慮
            float headerHeight = _rowHeight;
            
            // 可視範囲のセルのみ描画
            for (int row = _visibleRowStart; row < _visibleRowEnd && row < _sheetData.EditedValues.Count; row++)
            {
                float yPos = row * _rowHeight - _scrollPosition.y + headerHeight;
                
                // 行番号
                if (_showRowNumbers)
                {
                    var rowNumRect = new Rect(0, yPos, 50, _rowHeight);
                    GUI.Label(rowNumRect, (row + 1).ToString(), _headerStyle);
                }
                
                // セルデータ（可視範囲のみ）
                var rowData = _sheetData.EditedValues[row];
                if (rowData == null) continue;
                
                for (int col = _visibleColStart; col < _visibleColEnd && col < rowData.Count; col++)
                {
                    var xPosition = _columnStartPositions[col] - _scrollPosition.x;
                    var columnWidth = GetColumnWidth(col);
                    
                    if (xPosition + columnWidth < 0) continue;
                    if (xPosition > viewRect.width) break;
                    
                    var cellRect = new Rect(xPosition, yPos, columnWidth, _rowHeight);
                    var cellValue = rowData[col]?.ToString() ?? "";
                    
                    // スタイルの選択
                    var style = _cellStyle;
                    if (_selectedCells.Contains((row, col)))
                    {
                        style = _selectedCellStyle;
                    }
                    else if (IsSearchMatch(row, col))
                    {
                        style = _highlightStyle;
                    }
                    
                    GUI.Label(cellRect, cellValue, style);
                    
                    // セルの境界線（最小限）
                    if (row > 0 && col > 0)
                    {
                        DrawCellBorder(cellRect);
                    }
                }
            }
        }
        
        private void HandleMouseEvents(Rect viewRect)
        {
            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && viewRect.Contains(e.mousePosition))
            {
                var cellPos = GetCellFromMousePosition(e.mousePosition);
                if (cellPos.x >= 0 && cellPos.y >= 0)
                {
                    if (e.shift && _selectedCells.Count > 0)
                    {
                        // Shift+クリックで範囲選択
                        SelectRange(_selectionStart, cellPos);
                    }
                    else
                    {
                        // 通常クリックで単一選択
                        _selectedCells.Clear();
                        _selectionStart = cellPos;
                        _selectionEnd = cellPos;
                        _selectedCells.Add((cellPos.y, cellPos.x));
                    }
                    _isSelecting = true;
                    Repaint();
                }
            }
            else if (e.type == EventType.MouseDrag && _isSelecting)
            {
                var cellPos = GetCellFromMousePosition(e.mousePosition);
                if (cellPos.x >= 0 && cellPos.y >= 0)
                {
                    SelectRange(_selectionStart, cellPos);
                    Repaint();
                }
            }
            else if (e.type == EventType.MouseUp)
            {
                _isSelecting = false;
            }
        }
        
        private void HandleKeyboardEvents()
        {
            var e = Event.current;
            if (e.type == EventType.KeyDown)
            {
                // Ctrl/Cmd + C でコピー
                if ((e.control || e.command) && e.keyCode == KeyCode.C)
                {
                    CopySelectedCells();
                    e.Use();
                }
                // Ctrl/Cmd + A で全選択
                else if ((e.control || e.command) && e.keyCode == KeyCode.A)
                {
                    SelectAll();
                    e.Use();
                }
            }
        }
        
        private Vector2Int GetCellFromMousePosition(Vector2 mousePos)
        {
            // ヘッダー行の高さを考慮して行を計算
            int row = Mathf.FloorToInt((mousePos.y + _scrollPosition.y - _rowHeight) / _rowHeight);
            
            int col = -1;
            float xPos = mousePos.x + _scrollPosition.x;
            
            for (int i = 0; i < _sheetData.ColumnCount; i++)
            {
                if (xPos >= _columnStartPositions[i] && xPos < _columnStartPositions[i + 1])
                {
                    col = i;
                    break;
                }
            }
            
            if (row >= 0 && row < _sheetData.RowCount && col >= 0 && col < _sheetData.ColumnCount)
            {
                return new Vector2Int(col, row);
            }
            
            return new Vector2Int(-1, -1);
        }
        
        private void SelectRange(Vector2Int start, Vector2Int end)
        {
            _selectedCells.Clear();
            
            int minRow = Mathf.Min(start.y, end.y);
            int maxRow = Mathf.Max(start.y, end.y);
            int minCol = Mathf.Min(start.x, end.x);
            int maxCol = Mathf.Max(start.x, end.x);
            
            for (int row = minRow; row <= maxRow; row++)
            {
                for (int col = minCol; col <= maxCol; col++)
                {
                    _selectedCells.Add((row, col));
                }
            }
            
            _selectionEnd = end;
        }
        
        private void SelectAll()
        {
            _selectedCells.Clear();
            for (int row = 0; row < _sheetData.RowCount; row++)
            {
                for (int col = 0; col < _sheetData.ColumnCount; col++)
                {
                    _selectedCells.Add((row, col));
                }
            }
            Repaint();
        }
        
        private void CopySelectedCells()
        {
            if (_selectedCells.Count == 0) return;
            
            // 選択されたセルを行・列でソート
            var sortedCells = _selectedCells.OrderBy(c => c.row).ThenBy(c => c.col).ToList();
            
            // 最小・最大の行・列を取得
            int minRow = sortedCells.Min(c => c.row);
            int maxRow = sortedCells.Max(c => c.row);
            int minCol = sortedCells.Min(c => c.col);
            int maxCol = sortedCells.Max(c => c.col);
            
            var sb = new StringBuilder();
            
            for (int row = minRow; row <= maxRow; row++)
            {
                for (int col = minCol; col <= maxCol; col++)
                {
                    if (_selectedCells.Contains((row, col)))
                    {
                        if (_sheetData.EditedValues[row] != null && col < _sheetData.EditedValues[row].Count)
                        {
                            sb.Append(_sheetData.EditedValues[row][col]?.ToString() ?? "");
                        }
                    }
                    
                    if (col < maxCol)
                    {
                        sb.Append("\t");
                    }
                }
                
                if (row < maxRow)
                {
                    sb.AppendLine();
                }
            }
            
            GUIUtility.systemCopyBuffer = sb.ToString();
            Debug.Log($"コピーしました: {_selectedCells.Count} セル");
        }
        
        private float GetColumnWidth(int columnIndex)
        {
            if (_columnWidths.TryGetValue(columnIndex, out float width))
            {
                return width;
            }
            return _defaultColumnWidth;
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
            float xPos = _columnStartPositions[col];
            float yPos = row * _rowHeight;
            
            var viewRect = position;
            viewRect.height -= 40;
            
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
        
        private void DrawCellBorder(Rect rect)
        {
            var color = new Color(0.5f, 0.5f, 0.5f, 0.2f);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1, rect.y, 1, rect.height), color);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1, rect.width, 1), color);
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