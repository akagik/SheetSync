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
        private float _headerHeight = 25f; // ヘッダーの高さを別管理
        private Dictionary<int, float> _columnWidths = new Dictionary<int, float>();
        
        // 検索機能
        private string _searchText = "";
        private List<(int row, int col)> _searchResults = new List<(int row, int col)>();
        private int _currentSearchIndex = -1;
        
        // 表示設定
        private bool _showRowNumbers = true;
        private int _headerRowIndex = 0;
        
        // セル選択
        private bool _isSelecting = false;
        private Vector2Int _selectionStart;
        private Vector2Int _selectionEnd;
        private HashSet<(int row, int col)> _selectedCells = new HashSet<(int row, int col)>();
        private Vector2Int _currentCell = new Vector2Int(0, 0); // 現在のセル位置
        private bool _isRowSelectionMode = false; // 行選択モード
        
        // 仮想スクロール用
        private int _visibleRowStart = 0;
        private int _visibleRowEnd = 0;
        private int _drawnCellCount = 0; // デバッグ用
        
        // デバッグ設定
        private bool _showDebugInfo = false;
        
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
            // キーボードイベントを受け取れるようにフォーカスを設定
            wantsMouseMove = true;
        }
        
        private void InitializeStyles()
        {
            _headerStyle = new GUIStyle(EditorStyles.label)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { background = MakeTexture(2, 2, new Color(0.3f, 0.3f, 0.3f, 1f)) },
                fixedHeight = _headerHeight
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
            DrawDataGrid();
            
            // キーボードイベント処理
            HandleKeyboardEvents();
            
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
            
            // コピーボタン
            EditorGUI.BeginDisabledGroup(_selectedCells.Count == 0);
            if (GUILayout.Button("コピー", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                CopySelectedCells();
            }
            EditorGUI.EndDisabledGroup();
            
            // 表示設定
            _showRowNumbers = GUILayout.Toggle(_showRowNumbers, "行番号", EditorStyles.toolbarButton);
            _showDebugInfo = GUILayout.Toggle(_showDebugInfo, "デバッグ", EditorStyles.toolbarButton);
            
            // 情報表示
            if (_sheetData != null)
            {
                GUILayout.Label($"行: {_sheetData.RowCount}, 列: {_sheetData.ColumnCount}", EditorStyles.toolbarButton);
            }
            
            if (_headerRowIndex > 0)
            {
                GUILayout.Label($"ヘッダー行: {_headerRowIndex + 1}", EditorStyles.toolbarButton);
            }
            
            // デバッグ情報
            if (_showDebugInfo)
            {
                GUILayout.Label($"可視: {_visibleRowStart}-{_visibleRowEnd}, セル数: {_drawnCellCount}", EditorStyles.toolbarButton);
            }
            
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawDataGrid()
        {
            var viewRect = EditorGUILayout.GetControlRect(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            
            // ヘッダー領域とコンテンツ領域を分ける
            var headerRect = new Rect(viewRect.x, viewRect.y, viewRect.width, _headerHeight);
            var scrollRect = new Rect(viewRect.x, viewRect.y + _headerHeight, viewRect.width, viewRect.height - _headerHeight);
            
            // 全体のコンテンツサイズを計算
            float totalWidth = _showRowNumbers ? 50 : 0;
            for (int col = 0; col < _sheetData.ColumnCount; col++)
            {
                totalWidth += GetColumnWidth(col);
            }
            // ヘッダー行を除いたデータ行数
            int dataRowCount = _sheetData.RowCount - 1;
            float totalHeight = dataRowCount * _rowHeight; // データ行のみの高さ
            
            // ヘッダーを固定表示（スクロールの外）
            DrawFixedHeader(headerRect, totalWidth);
            
            // データ部分のスクロールビュー
            var contentRect = new Rect(0, 0, totalWidth, totalHeight);
            _scrollPosition = GUI.BeginScrollView(scrollRect, _scrollPosition, contentRect);
            
            // 可視範囲を計算
            CalculateVisibleRows(scrollRect);
            
            // 可視範囲のデータ行のみ描画
            _drawnCellCount = 0;
            DrawVisibleDataRows();
            
            // マウスイベント処理（スクロールビュー内で処理）
            HandleMouseEvents();
            
            GUI.EndScrollView();
            
            // デバッグ情報の描画
            if (_showDebugInfo)
            {
                DrawDebugInfo(viewRect);
            }
        }
        
        private void CalculateVisibleRows(Rect scrollRect)
        {
            // スクロール位置から可視範囲を計算
            int dataRowCount = _sheetData.RowCount - 1; // ヘッダー行を除く
            _visibleRowStart = Mathf.Max(0, Mathf.FloorToInt(_scrollPosition.y / _rowHeight));
            _visibleRowEnd = Mathf.Min(dataRowCount, Mathf.CeilToInt((_scrollPosition.y + scrollRect.height) / _rowHeight));
            
            // バッファを追加（スムーズなスクロールのため）
            _visibleRowStart = Mathf.Max(0, _visibleRowStart - 2);
            _visibleRowEnd = Mathf.Min(dataRowCount, _visibleRowEnd + 2);
        }
        
        private void DrawFixedHeader(Rect headerRect, float totalWidth)
        {
            // ヘッダー用のスクロールビュー（横スクロールのみ、スクロールバー非表示）
            var headerScrollPos = new Vector2(_scrollPosition.x, 0);
            var headerContentRect = new Rect(0, 0, totalWidth, _headerHeight);
            
            // スクロールバーを非表示にする（GUIStyle.noneを使用）
            GUI.BeginScrollView(headerRect, headerScrollPos, headerContentRect, GUIStyle.none, GUIStyle.none);
            
            float xPos = 0;
            
            // 行番号ヘッダー
            if (_showRowNumbers)
            {
                GUI.Label(new Rect(xPos, 0, 50, _headerHeight), "", _headerStyle);
                xPos += 50;
            }
            
            // 列ヘッダー
            for (int col = 0; col < _sheetData.ColumnCount; col++)
            {
                var columnWidth = GetColumnWidth(col);
                var rect = new Rect(xPos, 0, columnWidth, _headerHeight);
                
                string headerText = "";
                if (_headerRowIndex >= 0 && _headerRowIndex < _sheetData.EditedValues.Count && 
                    _sheetData.EditedValues[_headerRowIndex] != null && 
                    col < _sheetData.EditedValues[_headerRowIndex].Count)
                {
                    headerText = _sheetData.EditedValues[_headerRowIndex][col]?.ToString() ?? "";
                }
                
                GUI.Label(rect, headerText, _headerStyle);
                xPos += columnWidth;
            }
            
            GUI.EndScrollView();
        }
        
        private void DrawVisibleDataRows()
        {
            _drawnCellCount = 0;
            
            // ヘッダー行をスキップして、データ行のみを描画
            int dataStartRow = _headerRowIndex + 1;
            
            for (int i = _visibleRowStart; i < _visibleRowEnd; i++)
            {
                int dataRow = i + dataStartRow;
                if (dataRow >= _sheetData.EditedValues.Count) break;
                
                float xPos = 0;
                float yPos = i * _rowHeight;
                
                // 行番号（実際のスプレッドシート行番号を表示）
                if (_showRowNumbers)
                {
                    int actualRowNumber = i + _headerRowIndex + 2; // +1 for header, +1 for 1-based
                    GUI.Label(new Rect(xPos, yPos, 50, _rowHeight), actualRowNumber.ToString(), _headerStyle);
                    xPos += 50;
                }
                
                // セルデータ
                var rowData = _sheetData.EditedValues[dataRow];
                if (rowData != null)
                {
                    for (int col = 0; col < _sheetData.ColumnCount && col < rowData.Count; col++)
                    {
                        var columnWidth = GetColumnWidth(col);
                        var cellRect = new Rect(xPos, yPos, columnWidth, _rowHeight);
                        
                        var cellValue = rowData[col]?.ToString() ?? "";
                        
                        // スタイルの選択
                        var style = _cellStyle;
                        if (_selectedCells.Contains((i, col)))
                        {
                            style = _selectedCellStyle;
                        }
                        else if (IsSearchMatch(dataRow, col))
                        {
                            style = _highlightStyle;
                        }
                        
                        // 現在のセルには枠を描画
                        if (_currentCell.x == col && _currentCell.y == i)
                        {
                            EditorGUI.DrawRect(new Rect(cellRect.x, cellRect.y, cellRect.width, 2), Color.blue);
                            EditorGUI.DrawRect(new Rect(cellRect.x, cellRect.yMax - 2, cellRect.width, 2), Color.blue);
                            EditorGUI.DrawRect(new Rect(cellRect.x, cellRect.y, 2, cellRect.height), Color.blue);
                            EditorGUI.DrawRect(new Rect(cellRect.xMax - 2, cellRect.y, 2, cellRect.height), Color.blue);
                        }
                        
                        GUI.Label(cellRect, cellValue, style);
                        DrawCellBorder(cellRect);
                        
                        xPos += columnWidth;
                        _drawnCellCount++;
                    }
                }
            }
        }
        
        private void DrawDebugInfo(Rect viewRect)
        {
            var debugStyle = new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = Color.yellow },
                fontSize = 12
            };
            
            var debugRect = new Rect(viewRect.x + 10, viewRect.y + 10, 300, 100);
            GUI.Label(debugRect, $"Visible Rows: {_visibleRowStart} - {_visibleRowEnd}\nDrawn Cells: {_drawnCellCount}", debugStyle);
            
            // 可視範囲の境界線を描画
            var visibleStartY = _visibleRowStart * _rowHeight - _scrollPosition.y + _headerHeight;
            var visibleEndY = _visibleRowEnd * _rowHeight - _scrollPosition.y + _headerHeight;
            
            EditorGUI.DrawRect(new Rect(viewRect.x, viewRect.y + visibleStartY, viewRect.width, 2), Color.green);
            EditorGUI.DrawRect(new Rect(viewRect.x, viewRect.y + visibleEndY - 2, viewRect.width, 2), Color.red);
        }
        
        
        private void HandleMouseEvents()
        {
            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                // スクロールビュー内でのマウス位置
                // GUI.BeginScrollView内ではEvent.current.mousePositionは
                // コンテンツ座標系（スクロール位置を含む）での座標になる
                var mousePos = e.mousePosition;
                
                if (_showDebugInfo)
                {
                    Debug.Log($"Mouse: {mousePos}, Scroll: {_scrollPosition}");  
                }
                
                // 行番号エリアをクリックした場合
                if (_showRowNumbers && mousePos.x < 50)
                {
                    int row = Mathf.FloorToInt(mousePos.y / _rowHeight);
                    int dataRowCount = _sheetData.RowCount - 1;
                    if (row >= 0 && row < dataRowCount)
                    {
                        SelectEntireRow(row, e.shift);
                        _isRowSelectionMode = true;
                        _isSelecting = true;
                        e.Use();
                        Repaint();
                    }
                }
                else
                {
                    // セルをクリックした場合
                    var cellPos = GetCellFromMousePosition(mousePos);
                    if (cellPos.x >= 0 && cellPos.y >= 0)
                    {
                        _isRowSelectionMode = false;
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
                            _currentCell = cellPos;
                            _selectedCells.Add((cellPos.y, cellPos.x));
                        }
                        _isSelecting = true;
                        e.Use();
                        Repaint();
                    }
                }
            }
            else if (e.type == EventType.MouseDrag && _isSelecting)
            {
                if (_isRowSelectionMode)
                {
                    // 行選択モードでのドラッグ
                    var mousePos = e.mousePosition;
                    int row = Mathf.FloorToInt(mousePos.y / _rowHeight);
                    int dataRowCount = _sheetData.RowCount - 1;
                    if (row >= 0 && row < dataRowCount)
                    {
                        SelectRowRange(_selectionStart.y, row);
                        e.Use();
                        Repaint();
                    }
                }
                else
                {
                    // セル選択モードでのドラッグ
                    var cellPos = GetCellFromMousePosition(e.mousePosition);
                    if (cellPos.x >= 0 && cellPos.y >= 0)
                    {
                        SelectRange(_selectionStart, cellPos);
                        e.Use();
                        Repaint();
                    }
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
                // カーソルキーでの移動
                else if (e.keyCode == KeyCode.UpArrow)
                {
                    MoveCursor(0, -1, e.shift);
                    e.Use();
                }
                else if (e.keyCode == KeyCode.DownArrow)
                {
                    MoveCursor(0, 1, e.shift);
                    e.Use();
                }
                else if (e.keyCode == KeyCode.LeftArrow)
                {
                    MoveCursor(-1, 0, e.shift);
                    e.Use();
                }
                else if (e.keyCode == KeyCode.RightArrow)
                {
                    MoveCursor(1, 0, e.shift);
                    e.Use();
                }
            }
        }
        
        private void MoveCursor(int deltaX, int deltaY, bool extendSelection)
        {
            int dataRowCount = _sheetData.RowCount - 1;
            var newX = Mathf.Clamp(_currentCell.x + deltaX, 0, _sheetData.ColumnCount - 1);
            var newY = Mathf.Clamp(_currentCell.y + deltaY, 0, dataRowCount - 1);
            var newCell = new Vector2Int(newX, newY);
            
            if (extendSelection)
            {
                // Shift押下時は選択範囲を拡張
                SelectRange(_selectionStart, newCell);
            }
            else
            {
                // 通常移動
                _selectedCells.Clear();
                _selectedCells.Add((newY, newX));
                _selectionStart = newCell;
                _selectionEnd = newCell;
            }
            
            _currentCell = newCell;
            ScrollToCell(newY, newX);
            Repaint();
        }
        
        private void SelectEntireRow(int row, bool extend)
        {
            if (!extend)
            {
                _selectedCells.Clear();
                _selectionStart = new Vector2Int(0, row);
            }
            
            // 行全体を選択
            for (int col = 0; col < _sheetData.ColumnCount; col++)
            {
                _selectedCells.Add((row, col));
            }
            
            _selectionEnd = new Vector2Int(_sheetData.ColumnCount - 1, row);
            _currentCell = new Vector2Int(0, row);
        }
        
        private void SelectRowRange(int startRow, int endRow)
        {
            _selectedCells.Clear();
            
            int minRow = Mathf.Min(startRow, endRow);
            int maxRow = Mathf.Max(startRow, endRow);
            
            for (int row = minRow; row <= maxRow; row++)
            {
                for (int col = 0; col < _sheetData.ColumnCount; col++)
                {
                    _selectedCells.Add((row, col));
                }
            }
            
            _selectionEnd = new Vector2Int(_sheetData.ColumnCount - 1, endRow);
        }
        
        private Vector2Int GetCellFromMousePosition(Vector2 mousePos)
        {
            // データ領域内でのマウス位置
            // mousePos はスクロールビュー内でのコンテンツ座標系での位置
            int row = Mathf.FloorToInt(mousePos.y / _rowHeight);
            int dataRowCount = _sheetData.RowCount - 1;
            if (row < 0 || row >= dataRowCount)
                return new Vector2Int(-1, -1);
            
            // 列の検索
            float xPos = _showRowNumbers ? 50 : 0;
            for (int col = 0; col < _sheetData.ColumnCount; col++)
            {
                var columnWidth = GetColumnWidth(col);
                if (mousePos.x >= xPos && mousePos.x < xPos + columnWidth)
                {
                    return new Vector2Int(col, row);
                }
                xPos += columnWidth;
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
            int dataRowCount = _sheetData.RowCount - 1;
            for (int row = 0; row < dataRowCount; row++)
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
                        // データ行インデックスを実際のEditedValuesインデックスに変換
                        int actualRow = row + _headerRowIndex + 1;
                        if (actualRow < _sheetData.EditedValues.Count && 
                            _sheetData.EditedValues[actualRow] != null && 
                            col < _sheetData.EditedValues[actualRow].Count)
                        {
                            sb.Append(_sheetData.EditedValues[actualRow][col]?.ToString() ?? "");
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
            
            // ヘッダー行の次から検索開始
            for (int row = _headerRowIndex + 1; row < _sheetData.EditedValues.Count; row++)
            {
                var rowData = _sheetData.EditedValues[row];
                if (rowData == null) continue;
                
                for (int col = 0; col < rowData.Count; col++)
                {
                    var cellValue = rowData[col]?.ToString() ?? "";
                    if (cellValue.ToLower().Contains(searchLower))
                    {
                        // データ行インデックスとして保存（0ベース）
                        int dataRowIndex = row - _headerRowIndex - 1;
                        _searchResults.Add((dataRowIndex, col));
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
            
            float yPos = row * _rowHeight; // データ領域内での位置
            
            var viewRect = position;
            viewRect.height -= 40 + _headerHeight; // ツールバーとヘッダーの高さ
            
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
        
        private bool IsSearchMatch(int dataRow, int col)
        {
            if (_searchResults.Count == 0) return false;
            // dataRow は EditedValues のインデックスなので、データ行インデックスに変換
            int row = dataRow - _headerRowIndex - 1;
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