using System;
using System.Collections.Generic;
using System.Linq;

namespace SheetSync.Data
{
    /// <summary>
    /// Google Sheets のデータを直接参照するクラス
    /// メモリコピーを避けるため、元データへの参照を保持
    /// </summary>
    public class SheetData : ICsvData
    {
        private readonly IList<IList<object>> _values;
        private readonly int _rowOffset;
        private readonly int _colOffset;
        private readonly int _rowCount;
        private readonly int _colCount;
        
        /// <summary>
        /// コンストラクタ（全データ用）
        /// </summary>
        public SheetData(IList<IList<object>> values)
            : this(values, 0, 0, null, null)
        {
        }
        
        /// <summary>
        /// コンストラクタ（ビュー作成用）
        /// </summary>
        public SheetData(IList<IList<object>> values, 
                        int rowOffset, int colOffset,
                        int? rowCount, int? colCount)
        {
            _values = values ?? throw new ArgumentNullException(nameof(values));
            _rowOffset = rowOffset;
            _colOffset = colOffset;
            _rowCount = rowCount ?? CalculateRowCount(values, rowOffset);
            _colCount = colCount ?? CalculateColumnCount(values, rowOffset, _rowCount);
        }
        
        public int RowCount => _rowCount;
        public int ColumnCount => _colCount;
        
        public string GetCell(int row, int col)
        {
            int actualRow = row + _rowOffset;
            int actualCol = col + _colOffset;
            
            if (actualRow >= _values.Count) return "";
            var rowData = _values[actualRow];
            if (rowData == null || actualCol >= rowData.Count) return "";
            
            return rowData[actualCol]?.ToString() ?? "";
        }
        
        public void SetCell(int row, int col, string value)
        {
            int actualRow = row + _rowOffset;
            int actualCol = col + _colOffset;
            
            if (actualRow >= _values.Count) return;
            var rowData = _values[actualRow];
            if (rowData == null || actualCol >= rowData.Count) return;
            
            // IList<object> なので、object として設定
            rowData[actualCol] = value;
        }
        
        public ICsvData GetRowSlice(int startRow, int endRow = int.MaxValue)
        {
            // 新しいビューを作成（データのコピーなし）
            int actualEndRow = Math.Min(endRow, _rowCount);
            if (startRow >= actualEndRow) 
            {
                return new SheetData(new List<IList<object>>());
            }
            
            return new SheetData(_values, 
                _rowOffset + startRow, _colOffset,
                actualEndRow - startRow, _colCount);
        }
        
        public ICsvData GetColumnSlice(int startCol, int endCol = int.MaxValue)
        {
            // 新しいビューを作成（データのコピーなし）
            int actualEndCol = Math.Min(endCol, _colCount);
            if (startCol >= actualEndCol)
            {
                return new SheetData(new List<IList<object>>());
            }
            
            return new SheetData(_values,
                _rowOffset, _colOffset + startCol,
                _rowCount, actualEndCol - startCol);
        }
        
        public IEnumerable<string> GetRow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _rowCount)
                return Enumerable.Empty<string>();
            
            int actualRow = rowIndex + _rowOffset;
            if (actualRow >= _values.Count)
                return Enumerable.Empty<string>();
                
            var rowData = _values[actualRow];
            if (rowData == null)
                return Enumerable.Empty<string>();
            
            return Enumerable.Range(_colOffset, _colCount)
                .Select(col => col < rowData.Count ? rowData[col]?.ToString() ?? "" : "");
        }
        
        public IEnumerable<string> GetColumn(int colIndex)
        {
            if (colIndex < 0 || colIndex >= _colCount)
                return Enumerable.Empty<string>();
            
            int actualCol = colIndex + _colOffset;
            
            return Enumerable.Range(_rowOffset, _rowCount)
                .Select(row => 
                {
                    if (row >= _values.Count) return "";
                    var rowData = _values[row];
                    if (rowData == null || actualCol >= rowData.Count) return "";
                    return rowData[actualCol]?.ToString() ?? "";
                });
        }
        
        public void SetFromList(List<List<string>> data)
        {
            throw new NotSupportedException("SheetData は読み取り専用ビューです。新しいデータの設定はサポートされていません。");
        }
        
        public void SetFromListOfObjects(object table)
        {
            throw new NotSupportedException("SheetData は読み取り専用ビューです。新しいデータの設定はサポートされていません。");
        }
        
        public string ToCsvString()
        {
            var lines = new List<string>();
            
            for (int i = 0; i < _rowCount; i++)
            {
                var row = GetRow(i);
                var escapedValues = row.Select(value =>
                {
                    // CSV エスケープ処理
                    value = value.Replace("\"", "\"\"");
                    value = value.Replace("\r\n", "\n");
                    value = value.Replace("\n", "\\n");
                    return "\"" + value + "\"";
                });
                
                lines.Add(string.Join(", ", escapedValues));
            }
            
            return string.Join("\n", lines);
        }
        
        private static int CalculateRowCount(IList<IList<object>> values, int rowOffset)
        {
            if (values == null || values.Count <= rowOffset)
                return 0;
            return values.Count - rowOffset;
        }
        
        private static int CalculateColumnCount(IList<IList<object>> values, int rowOffset, int rowCount)
        {
            if (values == null || rowCount == 0)
                return 0;
                
            int maxColumns = 0;
            for (int i = rowOffset; i < rowOffset + rowCount && i < values.Count; i++)
            {
                if (values[i] != null && values[i].Count > maxColumns)
                {
                    maxColumns = values[i].Count;
                }
            }
            
            return maxColumns;
        }
    }
}