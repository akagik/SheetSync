using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using SheetSync.Services.Auth;
using SheetSync.Services;
using System.Linq;

namespace SheetSync.Services.Update
{
    /// <summary>
    /// サービスアカウント認証を使用したGoogleスプレッドシート更新サービス
    /// </summary>
    public class SheetUpdateServiceAccountService : ISheetUpdateService
    {
        #region 内部データ構造
        
        /// <summary>
        /// スプレッドシートのメタデータ
        /// </summary>
        private class SheetMetadata
        {
            public SheetsService Service { get; set; }
            public int? SheetId { get; set; }
            public IList<IList<object>> Values { get; set; }
            public IList<object> Headers { get; set; }
            public Dictionary<string, int> ColumnIndexMap { get; set; }
            public int KeyColumnIndex { get; set; }
        }
        
        #endregion
        
        /// <summary>
        /// スプレッドシートの特定の行を更新
        /// </summary>
        public async Task<bool> UpdateRowAsync(
            string spreadsheetId, 
            string sheetName, 
            string keyColumn, 
            string keyValue, 
            Dictionary<string, object> updateData,
            bool verbose = true)
        {
            // 単一行の更新を複数行更新メソッドで処理
            var updates = new Dictionary<string, Dictionary<string, object>>
            {
                [keyValue] = updateData
            };
            
            return await UpdateMultipleRowsAsync(spreadsheetId, sheetName, keyColumn, updates, verbose);
        }
        
        /// <summary>
        /// 複数行を一括更新（最適化版）
        /// 1回のAPI呼び出しですべての行を更新
        /// </summary>
        public async Task<bool> UpdateMultipleRowsAsync(
            string spreadsheetId,
            string sheetName,
            string keyColumn,
            Dictionary<string, Dictionary<string, object>> updates, 
            bool verbose = true)
        {
            try
            {
                // 初期検証
                if (!ValidateInputs(updates, verbose))
                {
                    return false;
                }
                
                // スプレッドシートのメタデータを取得
                var metadata = await GetSheetMetadataAsync(spreadsheetId, sheetName, keyColumn, verbose);
                if (metadata == null)
                {
                    return false;
                }
                
                // 更新対象列のインデックスマップを構築
                BuildColumnIndexMap(metadata, updates);
                
                // バッチ更新リクエストを作成
                var updateResult = BuildBatchUpdateRequests(metadata, keyColumn, updates, verbose);
                
                if (updateResult.Requests.Count == 0)
                {
                    if (verbose) Debug.LogWarning("有効な更新リクエストがありません。");
                    return false;
                }
                
                // バッチ更新を実行
                await ExecuteBatchUpdateAsync(metadata.Service, spreadsheetId, updateResult.Requests);
                
                // 結果をログ出力
                LogUpdateResults(updateResult, verbose);
                
                return updateResult.FailCount == 0;
            }
            catch (Exception ex)
            {
                if (verbose) Debug.LogError($"更新エラー: {ex.Message}");
                if (verbose) Debug.LogException(ex);
                return false;
            }
        }
        
        #region プライベートメソッド
        
        /// <summary>
        /// 入力パラメータの検証
        /// </summary>
        private bool ValidateInputs(Dictionary<string, Dictionary<string, object>> updates, bool verbose)
        {
            // サービスアカウント認証を確認
            if (!GoogleServiceAccountAuth.IsAuthenticated)
            {
                if (verbose) Debug.LogError("サービスアカウント認証が必要です。");
                return false;
            }
            
            if (updates == null || updates.Count == 0)
            {
                if (verbose) Debug.LogWarning("更新するデータがありません。");
                return true;
            }
            
            return true;
        }
        
        /// <summary>
        /// スプレッドシートのメタデータを取得
        /// </summary>
        private async Task<SheetMetadata> GetSheetMetadataAsync(
            string spreadsheetId, 
            string sheetName, 
            string keyColumn, 
            bool verbose)
        {
            var service = GoogleServiceAccountAuth.GetAuthenticatedService();
            
            // シート名からシートIDを取得
            var sheetId = await GoogleSheetsUtility.GetSheetIdFromNameAsync(service, spreadsheetId, sheetName);
            
            if (sheetId == null)
            {
                if (verbose) Debug.LogError($"シート '{sheetName}' のID取得に失敗しました。");
                return null;
            }
            
            // スプレッドシートのデータを取得
            var searchRange = $"{sheetName}!A:Z";
            var getRequest = service.Spreadsheets.Values.Get(spreadsheetId, searchRange);
            var response = await getRequest.ExecuteAsync();
            
            if (response.Values == null || response.Values.Count == 0)
            {
                if (verbose) Debug.LogError("スプレッドシートにデータが見つかりません。");
                return null;
            }
            
            // ヘッダー行を取得
            var headers = response.Values[0];
            
            // キー列のインデックスを見つける
            var keyColumnIndex = FindColumnIndex(headers, keyColumn);
            if (keyColumnIndex == -1)
            {
                if (verbose) Debug.LogError($"キー列 '{keyColumn}' が見つかりません。");
                return null;
            }
            
            return new SheetMetadata
            {
                Service = service,
                SheetId = sheetId,
                Values = response.Values,
                Headers = headers,
                KeyColumnIndex = keyColumnIndex,
                ColumnIndexMap = new Dictionary<string, int>()
            };
        }
        
        /// <summary>
        /// 列名からインデックスを検索
        /// </summary>
        private int FindColumnIndex(IList<object> headers, string columnName)
        {
            for (int i = 0; i < headers.Count; i++)
            {
                if (headers[i].ToString() == columnName)
                {
                    return i;
                }
            }
            return -1;
        }
        
        /// <summary>
        /// 更新対象列のインデックスマップを構築
        /// </summary>
        private void BuildColumnIndexMap(SheetMetadata metadata, Dictionary<string, Dictionary<string, object>> updates)
        {
            var allUpdateColumns = new HashSet<string>();
            foreach (var update in updates.Values)
            {
                foreach (var columnName in update.Keys)
                {
                    allUpdateColumns.Add(columnName);
                }
            }
            
            foreach (var columnName in allUpdateColumns)
            {
                var index = FindColumnIndex(metadata.Headers, columnName);
                if (index != -1)
                {
                    metadata.ColumnIndexMap[columnName] = index;
                }
            }
        }
        
        /// <summary>
        /// バッチ更新リクエストの構築結果
        /// </summary>
        private class BatchUpdateResult
        {
            public List<Request> Requests { get; set; } = new List<Request>();
            public int SuccessCount { get; set; }
            public int FailCount { get; set; }
            public List<string> FailedKeys { get; set; } = new List<string>();
        }
        
        /// <summary>
        /// バッチ更新リクエストを構築
        /// </summary>
        private BatchUpdateResult BuildBatchUpdateRequests(
            SheetMetadata metadata,
            string keyColumn,
            Dictionary<string, Dictionary<string, object>> updates,
            bool verbose)
        {
            var result = new BatchUpdateResult();
            
            foreach (var kvp in updates)
            {
                var keyValue = kvp.Key;
                var updateData = kvp.Value;
                
                // 該当する行を検索
                var targetRowIndex = FindTargetRow(metadata, keyValue);
                
                if (targetRowIndex == -1)
                {
                    if (verbose) Debug.LogWarning($"{keyColumn}='{keyValue}' の行が見つかりません。スキップします。");
                    result.FailCount++;
                    result.FailedKeys.Add(keyValue);
                    continue;
                }
                
                // この行の更新リクエストを作成
                bool hasValidUpdate = false;
                foreach (var updateKvp in updateData)
                {
                    var columnName = updateKvp.Key;
                    var value = updateKvp.Value;
                    
                    if (!metadata.ColumnIndexMap.ContainsKey(columnName))
                    {
                        if (verbose) Debug.LogWarning($"列 '{columnName}' が見つかりません。スキップします。");
                        continue;
                    }
                    
                    var columnIndex = metadata.ColumnIndexMap[columnName];
                    hasValidUpdate = true;
                    
                    // セルの更新リクエストを作成
                    result.Requests.Add(CreateCellUpdateRequest(
                        metadata.SheetId.Value,
                        targetRowIndex,
                        columnIndex,
                        value
                    ));
                }
                
                if (hasValidUpdate)
                {
                    result.SuccessCount++;
                }
                else
                {
                    result.FailCount++;
                    result.FailedKeys.Add(keyValue);
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// キー値に対応する行を検索
        /// </summary>
        private int FindTargetRow(SheetMetadata metadata, string keyValue)
        {
            for (int i = 1; i < metadata.Values.Count; i++)
            {
                var row = metadata.Values[i];
                if (row.Count > metadata.KeyColumnIndex && 
                    row[metadata.KeyColumnIndex].ToString() == keyValue)
                {
                    return i;
                }
            }
            return -1;
        }
        
        /// <summary>
        /// セル更新リクエストを作成
        /// </summary>
        private Request CreateCellUpdateRequest(int sheetId, int rowIndex, int columnIndex, object value)
        {
            return new Request
            {
                UpdateCells = new UpdateCellsRequest
                {
                    Range = new GridRange
                    {
                        SheetId = sheetId,
                        StartRowIndex = rowIndex,
                        EndRowIndex = rowIndex + 1,
                        StartColumnIndex = columnIndex,
                        EndColumnIndex = columnIndex + 1
                    },
                    Rows = new List<RowData>
                    {
                        new RowData
                        {
                            Values = new List<CellData>
                            {
                                new CellData
                                {
                                    UserEnteredValue = new ExtendedValue
                                    {
                                        StringValue = value?.ToString()
                                    }
                                }
                            }
                        }
                    },
                    Fields = "userEnteredValue"
                }
            };
        }
        
        /// <summary>
        /// バッチ更新を実行
        /// </summary>
        private async Task ExecuteBatchUpdateAsync(
            SheetsService service,
            string spreadsheetId,
            List<Request> requests)
        {
            var batchUpdateRequest = new BatchUpdateSpreadsheetRequest
            {
                Requests = requests
            };
            
            var updateRequest = service.Spreadsheets.BatchUpdate(batchUpdateRequest, spreadsheetId);
            await updateRequest.ExecuteAsync();
        }
        
        /// <summary>
        /// 更新結果をログ出力
        /// </summary>
        private void LogUpdateResults(BatchUpdateResult result, bool verbose)
        {
            if (!verbose) return;
            
            if (result.SuccessCount == 1 && result.FailCount == 0)
            {
                // 単一行の更新成功（UpdateRowAsyncからの呼び出し）
                Debug.Log($"更新成功: 行を更新しました。");
            }
            else
            {
                // 複数行の更新結果
                Debug.Log($"一括更新完了: 成功={result.SuccessCount}, 失敗={result.FailCount}");
                if (result.FailedKeys.Count > 0)
                {
                    Debug.LogWarning($"失敗したキー: {string.Join(", ", result.FailedKeys)}");
                }
            }
        }
        
        #endregion
    }
}