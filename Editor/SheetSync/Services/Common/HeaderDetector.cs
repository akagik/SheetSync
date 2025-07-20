using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace SheetSync.Services.Common
{
    /// <summary>
    /// スプレッドシートのヘッダー行を検出するユーティリティクラス
    /// </summary>
    public static class HeaderDetector
    {
        /// <summary>
        /// GlobalCCSettings を使用してヘッダー行のインデックスを取得
        /// </summary>
        /// <returns>ヘッダー行のインデックス（見つからない場合は0）</returns>
        public static int GetHeaderRowIndexFromSettings()
        {
            // GlobalCCSettings を検索
            var guids = AssetDatabase.FindAssets("t:GlobalCCSettings");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                var settings = AssetDatabase.LoadAssetAtPath<GlobalCCSettings>(path);
                if (settings != null)
                {
                    Debug.Log($"GlobalCCSettings の rowIndexOfName を使用: {settings.rowIndexOfName}");
                    return settings.rowIndexOfName;
                }
            }
            
            Debug.LogWarning("GlobalCCSettings が見つかりません。デフォルト値 0 を使用します。");
            return 0;
        }
        /// <summary>
        /// 指定されたキー列名を含むヘッダー行のインデックスを検出
        /// </summary>
        /// <param name="values">スプレッドシートの全データ</param>
        /// <param name="keyColumnName">検索するキー列名</param>
        /// <param name="maxRowsToCheck">チェックする最大行数（デフォルト: 20）</param>
        /// <returns>ヘッダー行のインデックス（見つからない場合は-1）</returns>
        public static int DetectHeaderRowIndex(IList<IList<object>> values, string keyColumnName, int maxRowsToCheck = 20)
        {
            if (values == null || values.Count == 0 || string.IsNullOrEmpty(keyColumnName))
            {
                return -1;
            }
            
            // 最大でmaxRowsToCheck行まで検索
            int rowsToCheck = System.Math.Min(values.Count, maxRowsToCheck);
            
            for (int i = 0; i < rowsToCheck; i++)
            {
                var row = values[i];
                if (row == null || row.Count == 0)
                {
                    continue;
                }
                
                // 行内でキー列名を検索
                for (int j = 0; j < row.Count; j++)
                {
                    var cellValue = row[j]?.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(cellValue) && cellValue.Equals(keyColumnName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.Log($"ヘッダー行を検出: 行 {i + 1} (0-based index: {i})");
                        return i;
                    }
                }
            }
            
            return -1;
        }
        
        /// <summary>
        /// 複数の候補列名からヘッダー行を検出
        /// </summary>
        /// <param name="values">スプレッドシートの全データ</param>
        /// <param name="candidateColumns">候補となる列名のリスト</param>
        /// <param name="requiredMatchCount">一致する必要がある最小列数（デフォルト: 2）</param>
        /// <param name="maxRowsToCheck">チェックする最大行数（デフォルト: 20）</param>
        /// <returns>ヘッダー行のインデックス（見つからない場合は-1）</returns>
        public static int DetectHeaderRowByMultipleColumns(
            IList<IList<object>> values, 
            string[] candidateColumns, 
            int requiredMatchCount = 2,
            int maxRowsToCheck = 20)
        {
            if (values == null || values.Count == 0 || candidateColumns == null || candidateColumns.Length == 0)
            {
                return -1;
            }
            
            int rowsToCheck = System.Math.Min(values.Count, maxRowsToCheck);
            
            for (int i = 0; i < rowsToCheck; i++)
            {
                var row = values[i];
                if (row == null || row.Count == 0)
                {
                    continue;
                }
                
                // この行で見つかった候補列の数をカウント
                int matchCount = 0;
                var rowValues = row.Select(cell => cell?.ToString()?.Trim() ?? "").ToList();
                
                foreach (var candidate in candidateColumns)
                {
                    if (rowValues.Any(value => value.Equals(candidate, System.StringComparison.OrdinalIgnoreCase)))
                    {
                        matchCount++;
                    }
                }
                
                // 必要な数以上の列が見つかった場合、この行をヘッダーとして採用
                if (matchCount >= requiredMatchCount)
                {
                    Debug.Log($"ヘッダー行を検出: 行 {i + 1} (一致した列数: {matchCount})");
                    return i;
                }
            }
            
            return -1;
        }
        
        /// <summary>
        /// ヘッダー行の可能性が高い行を推測（非空セルが多い行を選択）
        /// </summary>
        /// <param name="values">スプレッドシートの全データ</param>
        /// <param name="startRow">検索開始行（デフォルト: 0）</param>
        /// <param name="maxRowsToCheck">チェックする最大行数（デフォルト: 10）</param>
        /// <returns>ヘッダー行のインデックス</returns>
        public static int GuessHeaderRow(IList<IList<object>> values, int startRow = 0, int maxRowsToCheck = 10)
        {
            if (values == null || values.Count == 0)
            {
                return 0;
            }
            
            int bestRowIndex = 0;
            int maxNonEmptyCells = 0;
            int rowsToCheck = System.Math.Min(values.Count, startRow + maxRowsToCheck);
            
            for (int i = startRow; i < rowsToCheck; i++)
            {
                var row = values[i];
                if (row == null)
                {
                    continue;
                }
                
                // 非空セルの数をカウント
                int nonEmptyCells = row.Count(cell => !string.IsNullOrWhiteSpace(cell?.ToString()));
                
                // 文字列セルの割合も考慮（ヘッダーは通常文字列）
                int stringCells = row.Count(cell => cell is string && !string.IsNullOrWhiteSpace(cell.ToString()));
                
                // スコアを計算（非空セル数 + 文字列セル数のボーナス）
                int score = nonEmptyCells + (stringCells / 2);
                
                if (score > maxNonEmptyCells)
                {
                    maxNonEmptyCells = score;
                    bestRowIndex = i;
                }
            }
            
            Debug.Log($"推測されたヘッダー行: 行 {bestRowIndex + 1} (非空セル数: {maxNonEmptyCells})");
            return bestRowIndex;
        }
    }
}