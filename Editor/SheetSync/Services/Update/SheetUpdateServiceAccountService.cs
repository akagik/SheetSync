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
            try
            {
                // サービスアカウント認証を確認
                if (!GoogleServiceAccountAuth.IsAuthenticated)
                {
                    if (verbose) Debug.LogError("サービスアカウント認証が必要です。");
                    return false;
                }
                
                var service = GoogleServiceAccountAuth.GetAuthenticatedService();
                
                // シート名からシートIDを取得
                var sheetId = await GoogleSheetsUtility.GetSheetIdFromNameAsync(service, spreadsheetId, sheetName);
                
                if (sheetId == null)
                {
                    if (verbose) Debug.LogError($"シート '{sheetName}' のID取得に失敗しました。");
                    return false;
                }
                
                // まず、キー列の値で該当行を検索
                var searchRange = $"{sheetName}!A:Z";
                var getRequest = service.Spreadsheets.Values.Get(spreadsheetId, searchRange);
                var response = await getRequest.ExecuteAsync();
                
                if (response.Values == null || response.Values.Count == 0)
                {
                    if (verbose) Debug.LogError("スプレッドシートにデータが見つかりません。");
                    return false;
                }
                
                // ヘッダー行（最初の行）を取得
                var headers = response.Values[0];
                var keyColumnIndex = -1;
                
                // キー列のインデックスを見つける
                for (int i = 0; i < headers.Count; i++)
                {
                    if (headers[i].ToString() == keyColumn)
                    {
                        keyColumnIndex = i;
                        break;
                    }
                }
                
                if (keyColumnIndex == -1)
                {
                    if (verbose) Debug.LogError($"キー列 '{keyColumn}' が見つかりません。");
                    return false;
                }
                
                // 該当する行を検索
                int targetRowIndex = -1;
                for (int i = 1; i < response.Values.Count; i++)
                {
                    var row = response.Values[i];
                    if (row.Count > keyColumnIndex && row[keyColumnIndex].ToString() == keyValue)
                    {
                        targetRowIndex = i;
                        break;
                    }
                }
                
                if (targetRowIndex == -1)
                {
                    if (verbose) Debug.LogError($"{keyColumn}='{keyValue}' の行が見つかりません。");
                    return false;
                }
                
                // 更新データを準備
                var updateRequests = new List<Request>();
                
                foreach (var kvp in updateData)
                {
                    // 更新する列のインデックスを見つける
                    var columnIndex = -1;
                    for (int i = 0; i < headers.Count; i++)
                    {
                        if (headers[i].ToString() == kvp.Key)
                        {
                            columnIndex = i;
                            break;
                        }
                    }
                    
                    if (columnIndex == -1)
                    {
                        if (verbose) Debug.LogWarning($"列 '{kvp.Key}' が見つかりません。スキップします。");
                        continue;
                    }
                    
                    // セルの更新リクエストを作成
                    updateRequests.Add(new Request
                    {
                        UpdateCells = new UpdateCellsRequest
                        {
                            Range = new GridRange
                            {
                                SheetId = sheetId.Value,
                                StartRowIndex = targetRowIndex,
                                EndRowIndex = targetRowIndex + 1,
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
                                                StringValue = kvp.Value?.ToString()
                                            }
                                        }
                                    }
                                }
                            },
                            Fields = "userEnteredValue"
                        }
                    });
                }
                
                if (updateRequests.Count == 0)
                {
                    if (verbose) Debug.LogWarning("更新するデータがありません。");
                    return false;
                }
                
                // バッチ更新を実行
                var batchUpdateRequest = new BatchUpdateSpreadsheetRequest
                {
                    Requests = updateRequests
                };
                
                var updateRequest = service.Spreadsheets.BatchUpdate(batchUpdateRequest, spreadsheetId);
                await updateRequest.ExecuteAsync();
                
                if (verbose) Debug.Log($"更新成功: {keyColumn}='{keyValue}' の行を更新しました。");
                return true;
            }
            catch (Exception ex)
            {
                if (verbose) Debug.LogError($"更新エラー: {ex.Message}");
                if (verbose) Debug.LogException(ex);
                return false;
            }
        }
        
        /// <summary>
        /// 複数行を一括更新（最適化版）
        /// 1回のAPI呼び出しですべての行を更新
        /// </summary>
        public async Task<bool> UpdateMultipleRowsAsync(
            string spreadsheetId,
            string sheetName,
            string keyColumn,
            Dictionary<string, Dictionary<string, object>> updates, bool verbose = true)
        {
            try
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
                
                var service = GoogleServiceAccountAuth.GetAuthenticatedService();
                
                // シート名からシートIDを取得
                var sheetId = await GoogleSheetsUtility.GetSheetIdFromNameAsync(service, spreadsheetId, sheetName);
                
                if (sheetId == null)
                {
                    if (verbose) Debug.LogError($"シート '{sheetName}' のID取得に失敗しました。");
                    return false;
                }
                
                // スプレッドシートのデータを一度だけ取得
                var searchRange = $"{sheetName}!A:Z";
                var getRequest = service.Spreadsheets.Values.Get(spreadsheetId, searchRange);
                var response = await getRequest.ExecuteAsync();
                
                if (response.Values == null || response.Values.Count == 0)
                {
                    if (verbose) Debug.LogError("スプレッドシートにデータが見つかりません。");
                    return false;
                }
                
                // ヘッダー行（最初の行）を取得
                var headers = response.Values[0];
                
                // キー列のインデックスを見つける
                var keyColumnIndex = -1;
                for (int i = 0; i < headers.Count; i++)
                {
                    if (headers[i].ToString() == keyColumn)
                    {
                        keyColumnIndex = i;
                        break;
                    }
                }
                
                if (keyColumnIndex == -1)
                {
                    if (verbose) Debug.LogError($"キー列 '{keyColumn}' が見つかりません。");
                    return false;
                }
                
                // 更新対象の列名とインデックスのマッピングを作成
                var columnIndexMap = new Dictionary<string, int>();
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
                    for (int i = 0; i < headers.Count; i++)
                    {
                        if (headers[i].ToString() == columnName)
                        {
                            columnIndexMap[columnName] = i;
                            break;
                        }
                    }
                }
                
                // バッチ更新リクエストを準備
                var updateRequests = new List<Request>();
                var successCount = 0;
                var failCount = 0;
                var failedKeys = new List<string>();
                
                // 各更新対象行を処理
                foreach (var kvp in updates)
                {
                    var keyValue = kvp.Key;
                    var updateData = kvp.Value;
                    
                    // 該当する行を検索
                    int targetRowIndex = -1;
                    for (int i = 1; i < response.Values.Count; i++)
                    {
                        var row = response.Values[i];
                        if (row.Count > keyColumnIndex && row[keyColumnIndex].ToString() == keyValue)
                        {
                            targetRowIndex = i;
                            break;
                        }
                    }
                    
                    if (targetRowIndex == -1)
                    {
                        if (verbose) Debug.LogWarning($"{keyColumn}='{keyValue}' の行が見つかりません。スキップします。");
                        failCount++;
                        failedKeys.Add(keyValue);
                        continue;
                    }
                    
                    // この行の更新リクエストを作成
                    bool hasValidUpdate = false;
                    foreach (var updateKvp in updateData)
                    {
                        var columnName = updateKvp.Key;
                        var value = updateKvp.Value;
                        
                        if (!columnIndexMap.ContainsKey(columnName))
                        {
                            if (verbose) Debug.LogWarning($"列 '{columnName}' が見つかりません。スキップします。");
                            continue;
                        }
                        
                        var columnIndex = columnIndexMap[columnName];
                        hasValidUpdate = true;
                        
                        // セルの更新リクエストを作成
                        updateRequests.Add(new Request
                        {
                            UpdateCells = new UpdateCellsRequest
                            {
                                Range = new GridRange
                                {
                                    SheetId = sheetId.Value,
                                    StartRowIndex = targetRowIndex,
                                    EndRowIndex = targetRowIndex + 1,
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
                        });
                    }
                    
                    if (hasValidUpdate)
                    {
                        successCount++;
                    }
                    else
                    {
                        failCount++;
                        failedKeys.Add(keyValue);
                    }
                }
                
                if (updateRequests.Count == 0)
                {
                    if (verbose) Debug.LogWarning("有効な更新リクエストがありません。");
                    return false;
                }
                
                // バッチ更新を実行（1回のAPI呼び出し）
                var batchUpdateRequest = new BatchUpdateSpreadsheetRequest
                {
                    Requests = updateRequests
                };
                
                var updateRequest = service.Spreadsheets.BatchUpdate(batchUpdateRequest, spreadsheetId);
                await updateRequest.ExecuteAsync();
                
                if (verbose)
                {
                    Debug.Log($"一括更新完了: 成功={successCount}, 失敗={failCount}");
                    if (failedKeys.Count > 0)
                    {
                        Debug.LogWarning($"失敗したキー: {string.Join(", ", failedKeys)}");
                    }
                }
                
                return failCount == 0;
            }
            catch (Exception ex)
            {
                if (verbose) Debug.LogError($"一括更新エラー: {ex.Message}");
                if (verbose) Debug.LogException(ex);
                return false;
            }
        }
    }
}