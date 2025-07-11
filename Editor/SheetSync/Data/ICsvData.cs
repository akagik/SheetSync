using System;
using System.Collections.Generic;

namespace SheetSync.Data
{
    /// <summary>
    /// CSV データの抽象インターフェース
    /// ファイルベースと Google Sheets ベースの両方をサポート
    /// </summary>
    public interface ICsvData
    {
        // 基本プロパティ
        int RowCount { get; }
        int ColumnCount { get; }
        
        // セルアクセス
        string GetCell(int row, int col);
        void SetCell(int row, int col, string value);
        
        // スライス操作（ビューを返す）
        ICsvData GetRowSlice(int startRow, int endRow = int.MaxValue);
        ICsvData GetColumnSlice(int startCol, int endCol = int.MaxValue);
        
        // 行・列の取得
        IEnumerable<string> GetRow(int rowIndex);
        IEnumerable<string> GetColumn(int colIndex);
        
        // データ設定
        void SetFromList(List<List<string>> data);
        void SetFromListOfObjects(object table);
        
        // CSV 文字列への変換
        string ToCsvString();
    }
}