using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using SheetSync.Services.Auth;
using SheetSync.Services;
using Debug = UnityEngine.Debug;

namespace SheetSync.Services.Update
{
    /// <summary>
    /// OAuth2認証を使用したGoogle Spreadsheetsの更新サービス
    /// </summary>
    public class SheetUpdateOAuth2Service : ISheetUpdateService
    {
        private readonly ConvertSetting _setting;
        private readonly SheetsService _sheetsService;
        
        public SheetUpdateOAuth2Service(ConvertSetting setting)
        {
            _setting = setting ?? throw new ArgumentNullException(nameof(setting));
            
            if (!GoogleOAuth2Service.IsAuthenticated)
            {
                throw new InvalidOperationException("OAuth2認証が完了していません。先に認証を行ってください。");
            }
            
            _sheetsService = GoogleOAuth2Service.GetAuthenticatedService();
        }
        
        /// <summary>
        /// 単一行を更新する
        /// </summary>
        public async Task<UpdateResult> UpdateSingleRowAsync<T>(SimpleUpdateQuery<T> query) where T : class
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new UpdateResult();
            
            try
            {
                // 1. スプレッドシートのデータを取得
                var sheetData = await GetSheetDataAsync();
                if (sheetData == null || sheetData.Values == null || sheetData.Values.Count == 0)
                {
                    result.Success = false;
                    result.ErrorMessage = "スプレッドシートにデータがありません。";
                    return result;
                }
                
                // 2. ヘッダー行から列インデックスを取得
                var headers = sheetData.Values[0].Select(h => h?.ToString() ?? "").ToList();
                var typeRow = sheetData.Values[1].Select(t => t?.ToString() ?? "").ToList();
                
                int searchColumnIndex = headers.IndexOf(query.FieldName);
                int updateColumnIndex = headers.IndexOf(query.UpdateFieldName);
                
                if (searchColumnIndex < 0)
                {
                    result.Success = false;
                    result.ErrorMessage = $"フィールド '{query.FieldName}' が見つかりません。";
                    return result;
                }
                
                if (updateColumnIndex < 0)
                {
                    result.Success = false;
                    result.ErrorMessage = $"フィールド '{query.UpdateFieldName}' が見つかりません。";
                    return result;
                }
                
                // 3. 該当行を検索（ヘッダー2行をスキップ）
                int? targetRowIndex = null;
                object oldValue = null;
                
                for (int i = 2; i < sheetData.Values.Count; i++)
                {
                    var row = sheetData.Values[i];
                    if (row.Count > searchColumnIndex)
                    {
                        var cellValue = row[searchColumnIndex];
                        if (ValuesEqual(cellValue, query.SearchValue, typeRow[searchColumnIndex]))
                        {
                            targetRowIndex = i;
                            if (row.Count > updateColumnIndex)
                            {
                                oldValue = row[updateColumnIndex];
                            }
                            break;
                        }
                    }
                }
                
                if (!targetRowIndex.HasValue)
                {
                    result.Success = false;
                    result.ErrorMessage = $"{query.FieldName}={query.SearchValue} に一致する行が見つかりません。";
                    return result;
                }
                
                // 4. 差分確認（UIが有効な場合）
                if (Application.isBatchMode == false)
                {
                    var confirmed = EditorUtility.DisplayDialog(
                        "更新の確認",
                        $"以下の変更を適用しますか？\n\n" +
                        $"Row {targetRowIndex.Value + 1} ({query.FieldName}={query.SearchValue}):\n" +
                        $"  {query.UpdateFieldName}: \"{oldValue ?? "(empty)"}\" → \"{query.UpdateValue}\"",
                        "更新", "キャンセル");
                    
                    if (!confirmed)
                    {
                        result.Success = false;
                        result.ErrorMessage = "ユーザーによりキャンセルされました。";
                        return result;
                    }
                }
                
                // 5. 値を更新
                var sheetName = await GetSheetNameAsync();
                var updateRange = $"{sheetName}!{GetColumnLetter(updateColumnIndex)}{targetRowIndex.Value + 1}";
                var valueRange = new ValueRange
                {
                    Values = new List<IList<object>> { new List<object> { query.UpdateValue } }
                };
                
                var updateRequest = _sheetsService.Spreadsheets.Values.Update(valueRange, _setting.sheetID, updateRange);
                updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
                
                var response = await updateRequest.ExecuteAsync();
                
                // 6. 結果を設定
                result.Success = true;
                result.UpdatedRowCount = 1;
                result.UpdatedRows.Add(new UpdatedRowInfo
                {
                    RowNumber = targetRowIndex.Value + 1,
                    Changes = new Dictionary<string, FieldChange>
                    {
                        [query.UpdateFieldName] = new FieldChange
                        {
                            OldValue = oldValue,
                            NewValue = query.UpdateValue,
                            FieldType = query.UpdateValue?.GetType() ?? typeof(object)
                        }
                    }
                });
                
                Debug.Log($"更新成功: Row {targetRowIndex.Value + 1}, {query.UpdateFieldName}: \"{oldValue}\" → \"{query.UpdateValue}\"");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"更新中にエラーが発生しました: {ex.Message}";
                Debug.LogError(result.ErrorMessage);
                Debug.LogException(ex);
            }
            finally
            {
                stopwatch.Stop();
                result.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;
            }
            
            return result;
        }
        
        /// <summary>
        /// スプレッドシートのデータを取得
        /// </summary>
        private async Task<ValueRange> GetSheetDataAsync()
        {
            var sheetName = await GetSheetNameAsync();
            var range = $"{sheetName}!A:ZZ";
            var request = _sheetsService.Spreadsheets.Values.Get(_setting.sheetID, range);
            return await request.ExecuteAsync();
        }
        
        /// <summary>
        /// シート名を取得（gidから）
        /// </summary>
        private async Task<string> GetSheetNameAsync()
        {
            return await GoogleSheetsUtility.GetSheetNameFromSettingAsync(_sheetsService, _setting);
        }
        
        /// <summary>
        /// 列番号を列文字に変換（0 → A, 1 → B, 26 → AA）
        /// </summary>
        private string GetColumnLetter(int columnIndex)
        {
            string columnLetter = "";
            while (columnIndex >= 0)
            {
                columnLetter = (char)('A' + (columnIndex % 26)) + columnLetter;
                columnIndex = columnIndex / 26 - 1;
            }
            return columnLetter;
        }
        
        /// <summary>
        /// 値の比較（型を考慮）
        /// </summary>
        private bool ValuesEqual(object cellValue, object searchValue, string typeHint)
        {
            if (cellValue == null && searchValue == null) return true;
            if (cellValue == null || searchValue == null) return false;
            
            // 型ヒントに基づいて変換を試みる
            try
            {
                switch (typeHint.ToLower())
                {
                    case "int":
                    case "int32":
                        int intCell = Convert.ToInt32(cellValue);
                        int intSearch = Convert.ToInt32(searchValue);
                        return intCell == intSearch;
                        
                    case "string":
                        return cellValue.ToString() == searchValue.ToString();
                        
                    case "float":
                    case "double":
                        double doubleCell = Convert.ToDouble(cellValue);
                        double doubleSearch = Convert.ToDouble(searchValue);
                        return Math.Abs(doubleCell - doubleSearch) < 0.0001;
                        
                    default:
                        return cellValue.ToString() == searchValue.ToString();
                }
            }
            catch
            {
                // 変換に失敗した場合は文字列として比較
                return cellValue.ToString() == searchValue.ToString();
            }
        }
        
        /// <summary>
        /// スプレッドシートの特定の行を更新（ISheetUpdateService実装）
        /// </summary>
        public async Task<bool> UpdateRowAsync(
            string spreadsheetId, 
            string sheetName, 
            string keyColumn, 
            string keyValue, 
            Dictionary<string, object> updateData)
        {
            // 既存の更新ロジックを使用
            var query = new SimpleUpdateQuery<object>
            {
                FieldName = keyColumn,
                SearchValue = keyValue,
                UpdateFieldName = updateData.Keys.First(),
                UpdateValue = updateData.Values.First()
            };
            
            var result = await UpdateSingleRowAsync(query);
            return result.Success;
        }
        
        /// <summary>
        /// 複数行を一括更新（ISheetUpdateService実装）
        /// </summary>
        public async Task<bool> UpdateMultipleRowsAsync(
            string spreadsheetId,
            string sheetName,
            string keyColumn,
            Dictionary<string, Dictionary<string, object>> updates)
        {
            int successCount = 0;
            
            foreach (var kvp in updates)
            {
                var keyValue = kvp.Key;
                var updateData = kvp.Value;
                
                var result = await UpdateRowAsync(spreadsheetId, sheetName, keyColumn, keyValue, updateData);
                if (result)
                {
                    successCount++;
                }
            }
            
            Debug.Log($"一括更新完了: 成功={successCount}, 失敗={updates.Count - successCount}");
            return successCount == updates.Count;
        }
    }
}