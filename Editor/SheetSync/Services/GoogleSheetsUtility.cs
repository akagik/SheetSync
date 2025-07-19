using System;
using System.Threading.Tasks;
using Google.Apis.Sheets.v4;
using UnityEngine;

namespace SheetSync.Services
{
    /// <summary>
    /// Google Sheets API の共通ユーティリティ
    /// </summary>
    public static class GoogleSheetsUtility
    {
        /// <summary>
        /// GIDからシート名を取得する
        /// </summary>
        /// <param name="service">認証済みのSheetsService</param>
        /// <param name="spreadsheetId">スプレッドシートID</param>
        /// <param name="gid">シートのGID</param>
        /// <returns>シート名。見つからない場合はnull</returns>
        public static async Task<string> GetSheetNameFromGidAsync(SheetsService service, string spreadsheetId, string gid)
        {
            try
            {
                var spreadsheet = await service.Spreadsheets.Get(spreadsheetId).ExecuteAsync();
                
                // gidを数値に変換
                if (int.TryParse(gid, out int gidInt))
                {
                    foreach (var sheet in spreadsheet.Sheets)
                    {
                        if (sheet.Properties.SheetId == gidInt)
                        {
                            return sheet.Properties.Title;
                        }
                    }
                }
                
                // gidが見つからない場合は最初のシートを使用（警告を出す）
                if (spreadsheet.Sheets.Count > 0)
                {
                    var firstSheetName = spreadsheet.Sheets[0].Properties.Title;
                    Debug.LogWarning($"指定されたGID '{gid}' が見つかりません。最初のシート '{firstSheetName}' を使用します。");
                    return firstSheetName;
                }
                
                Debug.LogError($"スプレッドシート '{spreadsheetId}' にシートが存在しません。");
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"シート名の取得に失敗しました: {ex.Message}");
                Debug.LogException(ex);
                return null;
            }
        }
        
        /// <summary>
        /// ConvertSettingからシート名を取得する
        /// </summary>
        /// <param name="service">認証済みのSheetsService</param>
        /// <param name="setting">ConvertSetting</param>
        /// <returns>シート名。見つからない場合はnull</returns>
        public static async Task<string> GetSheetNameFromSettingAsync(SheetsService service, ConvertSetting setting)
        {
            if (setting == null)
            {
                Debug.LogError("ConvertSettingがnullです。");
                return null;
            }
            
            if (string.IsNullOrEmpty(setting.sheetID))
            {
                Debug.LogError("ConvertSettingにスプレッドシートIDが設定されていません。");
                return null;
            }
            
            return await GetSheetNameFromGidAsync(service, setting.sheetID, setting.gid);
        }
    }
}