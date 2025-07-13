using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
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
    /// Google Spreadsheetsの特定行を更新するサービス
    /// </summary>
    public class SheetUpdateService
    {
        private readonly string _apiKey;
        private readonly ConvertSetting _setting;
        private SheetsService _sheetsService;
        
        public SheetUpdateService(ConvertSetting setting)
        {
            _setting = setting ?? throw new ArgumentNullException(nameof(setting));
            _apiKey = EditorPrefs.GetString("SheetSync_ApiKey", "");
            
            if (string.IsNullOrEmpty(_apiKey))
            {
                // APIキーの入力を促す（1回のみ）
                bool apiKeySet = RequestApiKey();
                
                if (apiKeySet)
                {
                    // 再度取得
                    _apiKey = EditorPrefs.GetString("SheetSync_ApiKey", "");
                }
                
                if (string.IsNullOrEmpty(_apiKey))
                {
                    throw new InvalidOperationException("APIキーが設定されていません。Google Sheets機能を使用するには、有効なAPIキーが必要です。");
                }
            }
            
            InitializeService();
        }
        
        private bool RequestApiKey()
        {
            int dialogResult = EditorUtility.DisplayDialogComplex(
                "Google Sheets API キーが必要です",
                "Google Sheets API を使用するには API キーが必要です。\n" +
                "API キーを入力してください。",
                "入力する",
                "キャンセル",
                "ヘルプ"
            );
            
            if (dialogResult == 1) // キャンセル
            {
                return false;
            }
            else if (dialogResult == 2) // ヘルプ
            {
                Application.OpenURL("https://console.cloud.google.com/apis/credentials");
                // ヘルプを開いた後も入力を促す
                return RequestApiKeyInput();
            }
            else // 入力する
            {
                return RequestApiKeyInput();
            }
        }
        
        private bool RequestApiKeyInput()
        {
            const int maxRetries = 3;
            int retryCount = 0;
            
            while (retryCount < maxRetries)
            {
                string apiKey = EditorInputDialog.Show(
                    "API キー入力", 
                    retryCount == 0 
                        ? "Google API キーを入力してください:" 
                        : $"APIキーは空にできません。入力してください (試行 {retryCount + 1}/{maxRetries}):", 
                    "");
                
                if (!string.IsNullOrEmpty(apiKey))
                {
                    EditorPrefs.SetString("SheetSync_ApiKey", apiKey);
                    Debug.Log("APIキーを保存しました。");
                    return true;
                }
                
                retryCount++;
                
                if (retryCount < maxRetries)
                {
                    bool retry = EditorUtility.DisplayDialog(
                        "APIキーが入力されていません",
                        "APIキーは必須です。再度入力しますか？",
                        "はい",
                        "いいえ");
                    
                    if (!retry)
                    {
                        break;
                    }
                }
            }
            
            return false;
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
        /// 単一行を更新する（MVP実装）
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
        protected virtual async Task<ValueRange> GetSheetDataAsync()
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
        /// 保存されているAPIキーをクリアする
        /// </summary>
        [MenuItem("Tools/SheetSync/Clear API Key")]
        public static void ClearApiKey()
        {
            EditorPrefs.DeleteKey("SheetSync_ApiKey");
            Debug.Log("APIキーをクリアしました。");
        }
        
        /// <summary>
        /// APIキーが設定されているか確認する
        /// </summary>
        [MenuItem("Tools/SheetSync/Check API Key")]
        public static void CheckApiKey()
        {
            string apiKey = EditorPrefs.GetString("SheetSync_ApiKey", "");
            if (string.IsNullOrEmpty(apiKey))
            {
                Debug.Log("APIキーが設定されていません。");
            }
            else
            {
                Debug.Log($"APIキーが設定されています（最初の8文字: {apiKey.Substring(0, Math.Min(8, apiKey.Length))}...）");
            }
        }
    }
}