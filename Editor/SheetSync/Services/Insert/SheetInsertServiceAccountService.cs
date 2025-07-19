using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using SheetSync.Services.Auth;
using SheetSync.Services;
using System.Linq;

namespace SheetSync.Services.Insert
{
    /// <summary>
    /// サービスアカウント認証を使用したGoogleスプレッドシート行挿入サービス
    /// </summary>
    public class SheetInsertServiceAccountService : ISheetInsertService
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
        }
        
        /// <summary>
        /// バッチ挿入リクエストの構築結果
        /// </summary>
        private class BatchInsertResult
        {
            public List<Request> Requests { get; set; } = new List<Request>();
            public int SuccessCount { get; set; }
            public int FailCount { get; set; }
            public List<int> FailedIndices { get; set; } = new List<int>();
        }
        
        #endregion
        
        /// <summary>
        /// スプレッドシートに単一行を挿入
        /// </summary>
        public async Task<bool> InsertRowAsync(
            string spreadsheetId, 
            string sheetName, 
            int rowIndex,
            Dictionary<string, object> rowData,
            bool verbose = true)
        {
            // 単一行の挿入を複数行挿入メソッドで処理
            var insertions = new List<(int rowIndex, Dictionary<string, object> rowData)>
            {
                (rowIndex, rowData)
            };
            
            return await InsertMultipleRowsAsync(spreadsheetId, sheetName, insertions, verbose);
        }
        
        /// <summary>
        /// スプレッドシートに複数行を一括挿入
        /// 注意: 挿入は降順で実行され、行番号のずれを防ぎます
        /// </summary>
        public async Task<bool> InsertMultipleRowsAsync(
            string spreadsheetId,
            string sheetName,
            List<(int rowIndex, Dictionary<string, object> rowData)> insertions,
            bool verbose = true)
        {
            try
            {
                // 初期検証
                if (!ValidateInputs(insertions, verbose))
                {
                    return false;
                }
                
                // スプレッドシートのメタデータを取得
                var metadata = await GetSheetMetadataAsync(spreadsheetId, sheetName, verbose);
                if (metadata == null)
                {
                    return false;
                }
                
                // 挿入対象列のインデックスマップを構築
                BuildColumnIndexMap(metadata, insertions);
                
                // 挿入を降順でソート（行番号がずれないように）
                var sortedInsertions = insertions.OrderByDescending(i => i.rowIndex).ToList();
                
                // バッチ挿入リクエストを作成
                var insertResult = BuildBatchInsertRequests(metadata, sortedInsertions, verbose);
                
                if (insertResult.Requests.Count == 0)
                {
                    if (verbose) Debug.LogWarning("有効な挿入リクエストがありません。");
                    return false;
                }
                
                // バッチ挿入を実行
                await ExecuteBatchUpdateAsync(metadata.Service, spreadsheetId, insertResult.Requests);
                
                // 結果をログ出力
                LogInsertResults(insertResult, verbose);
                
                return insertResult.FailCount == 0;
            }
            catch (Exception ex)
            {
                if (verbose) Debug.LogError($"挿入エラー: {ex.Message}");
                if (verbose) Debug.LogException(ex);
                return false;
            }
        }
        
        #region プライベートメソッド
        
        /// <summary>
        /// 入力パラメータの検証
        /// </summary>
        private bool ValidateInputs(List<(int rowIndex, Dictionary<string, object> rowData)> insertions, bool verbose)
        {
            // サービスアカウント認証を確認
            if (!GoogleServiceAccountAuth.IsAuthenticated)
            {
                if (verbose) Debug.LogError("サービスアカウント認証が必要です。");
                return false;
            }
            
            if (insertions == null || insertions.Count == 0)
            {
                if (verbose) Debug.LogWarning("挿入するデータがありません。");
                return true;
            }
            
            // 行番号の検証
            foreach (var (rowIndex, rowData) in insertions)
            {
                if (rowIndex < 0)
                {
                    if (verbose) Debug.LogError($"無効な行番号: {rowIndex}。行番号は0以上である必要があります。");
                    return false;
                }
                
                if (rowData == null || rowData.Count == 0)
                {
                    if (verbose) Debug.LogWarning($"行 {rowIndex} の挿入データが空です。");
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// スプレッドシートのメタデータを取得
        /// </summary>
        private async Task<SheetMetadata> GetSheetMetadataAsync(
            string spreadsheetId, 
            string sheetName, 
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
            
            return new SheetMetadata
            {
                Service = service,
                SheetId = sheetId,
                Values = response.Values,
                Headers = headers,
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
        /// 挿入対象列のインデックスマップを構築
        /// </summary>
        private void BuildColumnIndexMap(SheetMetadata metadata, List<(int rowIndex, Dictionary<string, object> rowData)> insertions)
        {
            var allInsertColumns = new HashSet<string>();
            foreach (var (_, rowData) in insertions)
            {
                foreach (var columnName in rowData.Keys)
                {
                    allInsertColumns.Add(columnName);
                }
            }
            
            foreach (var columnName in allInsertColumns)
            {
                var index = FindColumnIndex(metadata.Headers, columnName);
                if (index != -1)
                {
                    metadata.ColumnIndexMap[columnName] = index;
                }
            }
        }
        
        /// <summary>
        /// バッチ挿入リクエストを構築
        /// </summary>
        private BatchInsertResult BuildBatchInsertRequests(
            SheetMetadata metadata,
            List<(int rowIndex, Dictionary<string, object> rowData)> sortedInsertions,
            bool verbose)
        {
            var result = new BatchInsertResult();
            
            foreach (var (rowIndex, rowData) in sortedInsertions)
            {
                // 挿入位置の検証（ヘッダー行の後、データ範囲内）
                var actualInsertIndex = rowIndex + 1; // ヘッダー行を考慮
                
                if (actualInsertIndex > metadata.Values.Count)
                {
                    if (verbose) Debug.LogWarning($"行 {rowIndex} は範囲外です（最大: {metadata.Values.Count - 1}）。スキップします。");
                    result.FailCount++;
                    result.FailedIndices.Add(rowIndex);
                    continue;
                }
                
                // 空行挿入リクエストを作成
                result.Requests.Add(CreateInsertRowRequest(metadata.SheetId.Value, actualInsertIndex));
                
                // 行データ設定リクエストを作成
                var cellValues = new List<CellData>();
                var hasValidData = false;
                
                // ヘッダーに基づいて各列のデータを準備
                for (int colIndex = 0; colIndex < metadata.Headers.Count; colIndex++)
                {
                    var columnName = metadata.Headers[colIndex].ToString();
                    CellData cellData;
                    
                    if (rowData.ContainsKey(columnName))
                    {
                        cellData = new CellData
                        {
                            UserEnteredValue = new ExtendedValue
                            {
                                StringValue = rowData[columnName]?.ToString()
                            }
                        };
                        hasValidData = true;
                    }
                    else
                    {
                        // 空のセル
                        cellData = new CellData();
                    }
                    
                    cellValues.Add(cellData);
                }
                
                if (hasValidData)
                {
                    // データ設定リクエストを追加
                    result.Requests.Add(CreateUpdateRowRequest(
                        metadata.SheetId.Value, 
                        actualInsertIndex, 
                        cellValues
                    ));
                    result.SuccessCount++;
                }
                else
                {
                    result.FailCount++;
                    result.FailedIndices.Add(rowIndex);
                    if (verbose) Debug.LogWarning($"行 {rowIndex} に有効なデータがありません。");
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// 行挿入リクエストを作成
        /// </summary>
        private Request CreateInsertRowRequest(int sheetId, int rowIndex)
        {
            return new Request
            {
                InsertDimension = new InsertDimensionRequest
                {
                    Range = new DimensionRange
                    {
                        SheetId = sheetId,
                        Dimension = "ROWS",
                        StartIndex = rowIndex,
                        EndIndex = rowIndex + 1
                    },
                    InheritFromBefore = false
                }
            };
        }
        
        /// <summary>
        /// 行データ更新リクエストを作成
        /// </summary>
        private Request CreateUpdateRowRequest(int sheetId, int rowIndex, List<CellData> cellValues)
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
                        StartColumnIndex = 0,
                        EndColumnIndex = cellValues.Count
                    },
                    Rows = new List<RowData>
                    {
                        new RowData
                        {
                            Values = cellValues
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
        /// 挿入結果をログ出力
        /// </summary>
        private void LogInsertResults(BatchInsertResult result, bool verbose)
        {
            if (!verbose) return;
            
            if (result.SuccessCount == 1 && result.FailCount == 0)
            {
                // 単一行の挿入成功
                Debug.Log($"挿入成功: 行を挿入しました。");
            }
            else
            {
                // 複数行の挿入結果
                Debug.Log($"一括挿入完了: 成功={result.SuccessCount}, 失敗={result.FailCount}");
                if (result.FailedIndices.Count > 0)
                {
                    Debug.LogWarning($"失敗した行番号: {string.Join(", ", result.FailedIndices)}");
                }
            }
        }
        
        #endregion
    }
}