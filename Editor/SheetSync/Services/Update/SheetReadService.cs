using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Google.Apis.Services;
using Debug = UnityEngine.Debug;

namespace SheetSync.Services.Update
{
    /// <summary>
    /// Google Spreadsheetsから特定の行を読み取るサービス（APIキー対応）
    /// </summary>
    public class SheetReadService
    {
        private readonly string _apiKey;
        private readonly ConvertSetting _setting;
        private SheetsService _sheetsService;
        
        public SheetReadService(ConvertSetting setting)
        {
            _setting = setting ?? throw new ArgumentNullException(nameof(setting));
            _apiKey = EditorPrefs.GetString("SheetSync_ApiKey", "");
            
            if (string.IsNullOrEmpty(_apiKey))
            {
                throw new InvalidOperationException("APIキーが設定されていません。");
            }
            
            InitializeService();
        }
        
        private void InitializeService()
        {
            _sheetsService = new SheetsService(new BaseClientService.Initializer
            {
                ApiKey = _apiKey,
                ApplicationName = "SheetSync"
            });
        }
        
        /// <summary>
        /// 特定の条件に一致する行を検索して読み取る
        /// </summary>
        public async Task<ReadResult> SearchAndReadAsync<T>(SimpleUpdateQuery<T> query) where T : class
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new ReadResult();
            
            try
            {
                // スプレッドシートのデータを取得
                var sheetData = await GetSheetDataAsync();
                if (sheetData == null || sheetData.Values == null || sheetData.Values.Count == 0)
                {
                    result.Success = false;
                    result.ErrorMessage = "スプレッドシートにデータがありません。";
                    return result;
                }
                
                // ヘッダー行から列インデックスを取得
                var headers = sheetData.Values[0].Select(h => h?.ToString() ?? "").ToList();
                var typeRow = sheetData.Values[1].Select(t => t?.ToString() ?? "").ToList();
                
                int searchColumnIndex = headers.IndexOf(query.FieldName);
                
                if (searchColumnIndex < 0)
                {
                    result.Success = false;
                    result.ErrorMessage = $"フィールド '{query.FieldName}' が見つかりません。";
                    return result;
                }
                
                // 該当行を検索（ヘッダー2行をスキップ）
                for (int i = 2; i < sheetData.Values.Count; i++)
                {
                    var row = sheetData.Values[i];
                    if (row.Count > searchColumnIndex)
                    {
                        var cellValue = row[searchColumnIndex];
                        if (ValuesEqual(cellValue, query.SearchValue, typeRow[searchColumnIndex]))
                        {
                            // 行データを構築
                            var rowData = new Dictionary<string, object>();
                            for (int j = 0; j < headers.Count && j < row.Count; j++)
                            {
                                rowData[headers[j]] = row[j];
                            }
                            
                            result.FoundRows.Add(new FoundRowInfo
                            {
                                RowNumber = i + 1,
                                Data = rowData
                            });
                        }
                    }
                }
                
                result.Success = true;
                result.TotalRowsFound = result.FoundRows.Count;
                
                if (result.TotalRowsFound > 0)
                {
                    Debug.Log($"検索成功: {result.TotalRowsFound} 行が見つかりました。");
                }
                else
                {
                    Debug.LogWarning($"{query.FieldName}={query.SearchValue} に一致する行が見つかりません。");
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"読み取り中にエラーが発生しました: {ex.Message}";
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
            try
            {
                var spreadsheet = await _sheetsService.Spreadsheets.Get(_setting.sheetID).ExecuteAsync();
                
                // gidを数値に変換
                if (int.TryParse(_setting.gid, out int gidInt))
                {
                    foreach (var sheet in spreadsheet.Sheets)
                    {
                        if (sheet.Properties.SheetId == gidInt)
                        {
                            return sheet.Properties.Title;
                        }
                    }
                }
                
                // gidが見つからない場合は最初のシートを使用
                if (spreadsheet.Sheets.Count > 0)
                {
                    Debug.LogWarning($"指定されたgid '{_setting.gid}' が見つかりません。最初のシートを使用します。");
                    return spreadsheet.Sheets[0].Properties.Title;
                }
                
                throw new InvalidOperationException("スプレッドシートにシートが存在しません。");
            }
            catch (Exception ex)
            {
                Debug.LogError($"シート名の取得に失敗しました: {ex.Message}");
                // フォールバック
                return "Sheet1";
            }
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
    }
    
    /// <summary>
    /// 読み取り操作の結果
    /// </summary>
    public class ReadResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public int TotalRowsFound { get; set; }
        public List<FoundRowInfo> FoundRows { get; set; } = new List<FoundRowInfo>();
        public long ElapsedMilliseconds { get; set; }
    }
    
    /// <summary>
    /// 見つかった行の情報
    /// </summary>
    public class FoundRowInfo
    {
        public int RowNumber { get; set; }
        public Dictionary<string, object> Data { get; set; }
    }
}