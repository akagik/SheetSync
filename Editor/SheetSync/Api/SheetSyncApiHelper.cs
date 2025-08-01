using System;
using UnityEngine;
using Google.Apis.Sheets.v4;
using SheetSync.Services.Auth;

namespace SheetSync.Api
{
    /// <summary>
    /// SheetSyncApi用のヘルパークラス
    /// メインスレッドで実行される同期的なメソッドを提供
    /// </summary>
    public static class SheetSyncApiHelper
    {
        /// <summary>
        /// GIDからシート名を同期的に取得（テスト用）
        /// </summary>
        public static string TestGetSheetNameFromGid(string spreadsheetId, string gid)
        {
            try
            {
                if (!GoogleServiceAccountAuth.IsAuthenticated)
                {
                    Debug.LogError("Service account authentication required");
                    return null;
                }
                
                var service = GoogleServiceAccountAuth.GetAuthenticatedService();
                var spreadsheet = service.Spreadsheets.Get(spreadsheetId).Execute();
                
                // gidを数値に変換
                if (int.TryParse(gid, out int gidInt))
                {
                    foreach (var sheet in spreadsheet.Sheets)
                    {
                        if (sheet.Properties.SheetId == gidInt)
                        {
                            Debug.Log($"Found sheet: GID={gid}, Name={sheet.Properties.Title}");
                            return sheet.Properties.Title;
                        }
                    }
                }
                
                // gidが見つからない場合は最初のシートを使用
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
    }
}