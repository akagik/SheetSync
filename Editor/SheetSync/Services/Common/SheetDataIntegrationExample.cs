using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Google.Apis.Sheets.v4;
using SheetSync.Services.Auth;
using SheetSync.Services.Update;
using SheetSync.Services.Insert;

namespace SheetSync.Services.Common
{
    /// <summary>
    /// ExtendedSheetDataを使用したサービス統合の例
    /// SheetUpdateServiceAccountService や SheetInsertServiceAccountService との連携方法を示す
    /// </summary>
    public static class SheetDataIntegrationExample
    {
        /// <summary>
        /// ExtendedSheetDataを使用してスプレッドシートを更新する例
        /// </summary>
        public static async Task<bool> UpdateSpreadsheetWithSheetData(
            string spreadsheetId,
            string sheetName,
            ExtendedSheetData sheetData,
            bool verbose = true)
        {
            try
            {
                // 1. SheetDataから変更を収集
                var changes = sheetData.Changes;
                if (changes.Count == 0)
                {
                    if (verbose) Debug.Log("変更がありません。");
                    return true;
                }
                
                // 2. 変更を種類ごとに分類
                var updates = new Dictionary<string, Dictionary<string, object>>();
                var insertions = new List<(int rowIndex, Dictionary<string, object> rowData)>();
                var deletions = new List<int>();
                
                foreach (var change in changes)
                {
                    switch (change.Type)
                    {
                        case ExtendedSheetData.ChangeType.Update:
                            // 更新の場合は行ごとにグループ化
                            var rowKey = GetRowKey(sheetData, change.RowIndex);
                            if (!string.IsNullOrEmpty(rowKey))
                            {
                                if (!updates.ContainsKey(rowKey))
                                {
                                    updates[rowKey] = new Dictionary<string, object>();
                                }
                                updates[rowKey][change.ColumnName] = change.NewValue;
                            }
                            break;
                            
                        case ExtendedSheetData.ChangeType.Insert:
                            insertions.Add((change.RowIndex, change.RowData));
                            break;
                            
                        case ExtendedSheetData.ChangeType.Delete:
                            deletions.Add(change.RowIndex);
                            break;
                    }
                }
                
                // 3. 各種サービスを使用して変更を適用
                bool success = true;
                
                // 削除を先に実行（インデックスのずれを防ぐため降順で）
                if (deletions.Count > 0)
                {
                    deletions.Sort((a, b) => b.CompareTo(a));
                    foreach (var rowIndex in deletions)
                    {
                        // TODO: DeleteServiceの実装が必要
                        if (verbose) Debug.LogWarning($"行削除はまだ実装されていません: 行 {rowIndex}");
                    }
                }
                
                // 挿入を実行
                if (insertions.Count > 0)
                {
                    var insertService = new SheetInsertServiceAccountService();
                    success &= await insertService.InsertMultipleRowsAsync(
                        spreadsheetId, sheetName, insertions, verbose);
                }
                
                // 更新を実行
                if (updates.Count > 0)
                {
                    var updateService = new SheetUpdateServiceAccountService();
                    // 仮にIDカラムをキーとして使用
                    success &= await updateService.UpdateMultipleRowsAsync(
                        spreadsheetId, sheetName, "ID", updates, verbose);
                }
                
                if (success && verbose)
                {
                    Debug.Log($"スプレッドシートの更新が完了しました: " +
                             $"更新={updates.Count}行, 挿入={insertions.Count}行, 削除={deletions.Count}行");
                }
                
                return success;
            }
            catch (Exception ex)
            {
                if (verbose) Debug.LogError($"スプレッドシート更新エラー: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// スプレッドシートからExtendedSheetDataを作成する例
        /// </summary>
        public static async Task<ExtendedSheetData> LoadSheetDataAsync(
            string spreadsheetId,
            string sheetName,
            bool verbose = true)
        {
            try
            {
                if (!GoogleServiceAccountAuth.IsAuthenticated)
                {
                    if (verbose) Debug.LogError("サービスアカウント認証が必要です。");
                    return null;
                }
                
                var service = GoogleServiceAccountAuth.GetAuthenticatedService();
                
                // データを取得
                var range = $"{sheetName}!A:Z";
                var request = service.Spreadsheets.Values.Get(spreadsheetId, range);
                var response = await request.ExecuteAsync();
                
                if (response.Values == null || response.Values.Count == 0)
                {
                    if (verbose) Debug.LogError("データが見つかりません。");
                    return null;
                }
                
                // ExtendedSheetDataを作成
                var sheetData = new ExtendedSheetData(response.Values);
                
                // まず GlobalCCSettings から取得を試みる
                var headerRowIndex = HeaderDetector.GetHeaderRowIndexFromSettings();
                
                // 設定値が範囲外の場合は自動検出
                if (headerRowIndex >= response.Values.Count || headerRowIndex < 0)
                {
                    if (verbose) Debug.LogWarning($"GlobalCCSettings の rowIndexOfName ({headerRowIndex}) が範囲外です。自動検出を行います。");
                    
                    // ヘッダー行を検出してビューを作成
                    var commonColumns = new[] { "key", "id", "name", "ja", "en", "ko" };
                    headerRowIndex = HeaderDetector.DetectHeaderRowByMultipleColumns(response.Values, commonColumns, 2);
                    
                    if (headerRowIndex == -1)
                    {
                        headerRowIndex = HeaderDetector.GuessHeaderRow(response.Values);
                    }
                }
                
                if (headerRowIndex > 0)
                {
                    // ヘッダー行が最初の行でない場合は、ヘッダー行から始まるビューを作成
                    var dataFromHeader = response.Values.Skip(headerRowIndex).ToList();
                    sheetData = new ExtendedSheetData(dataFromHeader);
                }
                
                if (verbose)
                {
                    Debug.Log($"SheetDataを読み込みました: {sheetData.RowCount}行 x {sheetData.ColumnCount}列 (ヘッダー行: {headerRowIndex + 1})");
                }
                
                return sheetData;
            }
            catch (Exception ex)
            {
                if (verbose) Debug.LogError($"SheetData読み込みエラー: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// 使用例: データの編集と差分表示
        /// </summary>
        public static async Task<bool> ExampleEditAndShowDiff(
            string spreadsheetId,
            string sheetName)
        {
            // 1. データを読み込み
            var sheetData = await LoadSheetDataAsync(spreadsheetId, sheetName);
            if (sheetData == null) return false;
            
            // 2. キーインデックスを構築
            sheetData.BuildKeyIndex("ID");
            
            // 3. データを編集
            // 例: ID=2の行の名前と年齢を更新
            sheetData.UpdateRowByKey("ID", "2", new Dictionary<string, object>
            {
                ["Name"] = "Bob Updated",
                ["Age"] = "31"
            });
            
            // 例: 新しい行を挿入
            sheetData.InsertRow(4, new Dictionary<string, object>
            {
                ["ID"] = "10",
                ["Name"] = "New Person",
                ["Age"] = "25"
            });
            
            // 4. 差分を表示
            Debug.Log("=== 変更差分 ===");
            foreach (var change in sheetData.Changes)
            {
                switch (change.Type)
                {
                    case ExtendedSheetData.ChangeType.Update:
                        Debug.Log($"[更新] 行{change.RowIndex}, {change.ColumnName}: " +
                                 $"{change.OldValue} → {change.NewValue}");
                        break;
                        
                    case ExtendedSheetData.ChangeType.Insert:
                        Debug.Log($"[挿入] 行{change.RowIndex}: " +
                                 string.Join(", ", change.RowData));
                        break;
                        
                    case ExtendedSheetData.ChangeType.Delete:
                        Debug.Log($"[削除] 行{change.RowIndex}");
                        break;
                }
            }
            
            // 5. ユーザーに確認を求める（実際のUIでは確認ダイアログを表示）
            Debug.Log("変更を適用しますか？ (この例では自動的に適用されます)");
            
            // 6. 変更を適用
            return await UpdateSpreadsheetWithSheetData(spreadsheetId, sheetName, sheetData);
        }
        
        /// <summary>
        /// 行のキー値を取得するヘルパーメソッド
        /// </summary>
        private static string GetRowKey(ExtendedSheetData sheetData, int rowIndex)
        {
            // この例ではIDカラムを使用
            var idColumnIndex = sheetData.GetColumnIndex("ID");
            if (idColumnIndex >= 0 && rowIndex < sheetData.EditedValues.Count)
            {
                var row = sheetData.EditedValues[rowIndex];
                if (row != null && idColumnIndex < row.Count)
                {
                    return row[idColumnIndex]?.ToString();
                }
            }
            return null;
        }
    }
}