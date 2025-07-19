using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using SheetSync.Services.Auth;

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
            Dictionary<string, object> updateData)
        {
            try
            {
                // サービスアカウント認証を確認
                if (!GoogleServiceAccountAuth.IsAuthenticated)
                {
                    Debug.LogError("サービスアカウント認証が必要です。");
                    return false;
                }
                
                var service = GoogleServiceAccountAuth.GetAuthenticatedService();
                
                // まず、キー列の値で該当行を検索
                var searchRange = $"{sheetName}!A:Z";
                var getRequest = service.Spreadsheets.Values.Get(spreadsheetId, searchRange);
                var response = await getRequest.ExecuteAsync();
                
                if (response.Values == null || response.Values.Count == 0)
                {
                    Debug.LogError("スプレッドシートにデータが見つかりません。");
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
                    Debug.LogError($"キー列 '{keyColumn}' が見つかりません。");
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
                    Debug.LogError($"{keyColumn}='{keyValue}' の行が見つかりません。");
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
                        Debug.LogWarning($"列 '{kvp.Key}' が見つかりません。スキップします。");
                        continue;
                    }
                    
                    // セルの更新リクエストを作成
                    updateRequests.Add(new Request
                    {
                        UpdateCells = new UpdateCellsRequest
                        {
                            Range = new GridRange
                            {
                                SheetId = 0, // デフォルトシート
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
                    Debug.LogWarning("更新するデータがありません。");
                    return false;
                }
                
                // バッチ更新を実行
                var batchUpdateRequest = new BatchUpdateSpreadsheetRequest
                {
                    Requests = updateRequests
                };
                
                var updateRequest = service.Spreadsheets.BatchUpdate(batchUpdateRequest, spreadsheetId);
                await updateRequest.ExecuteAsync();
                
                Debug.Log($"更新成功: {keyColumn}='{keyValue}' の行を更新しました。");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"更新エラー: {ex.Message}");
                Debug.LogException(ex);
                return false;
            }
        }
        
        /// <summary>
        /// 複数行を一括更新
        /// </summary>
        public async Task<bool> UpdateMultipleRowsAsync(
            string spreadsheetId,
            string sheetName,
            string keyColumn,
            Dictionary<string, Dictionary<string, object>> updates)
        {
            int successCount = 0;
            int failCount = 0;
            
            foreach (var kvp in updates)
            {
                var keyValue = kvp.Key;
                var updateData = kvp.Value;
                
                var result = await UpdateRowAsync(spreadsheetId, sheetName, keyColumn, keyValue, updateData);
                if (result)
                {
                    successCount++;
                }
                else
                {
                    failCount++;
                }
            }
            
            Debug.Log($"一括更新完了: 成功={successCount}, 失敗={failCount}");
            return failCount == 0;
        }
    }
}