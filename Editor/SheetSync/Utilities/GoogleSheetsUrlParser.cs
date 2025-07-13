using System;
using System.Text.RegularExpressions;
using UnityEngine;

namespace SheetSync
{
    /// <summary>
    /// Google SpreadsheetsのURLを解析してSheetIDとGIDを抽出するユーティリティ
    /// </summary>
    public static class GoogleSheetsUrlParser
    {
        // Google SpreadsheetsのURLパターン
        // https://docs.google.com/spreadsheets/d/{SHEET_ID}/edit?gid={GID}#gid={GID}
        private const string SHEET_ID_PATTERN = @"/spreadsheets/d/([a-zA-Z0-9-_]+)";
        private const string GID_PATTERN = @"[?&#]gid=([0-9]+)";
        
        /// <summary>
        /// URLからSheet情報を抽出
        /// </summary>
        public class SheetInfo
        {
            public string SheetId { get; set; }
            public string Gid { get; set; }
            public bool IsValid => !string.IsNullOrEmpty(SheetId) && !string.IsNullOrEmpty(Gid);
            
            public SheetInfo(string sheetId = "", string gid = "")
            {
                SheetId = sheetId;
                Gid = gid;
            }
        }
        
        /// <summary>
        /// Google SpreadsheetsのURLを解析してSheetIDとGIDを抽出
        /// </summary>
        /// <param name="url">Google SpreadsheetsのURL</param>
        /// <returns>抽出されたSheetInfo（失敗時はnull）</returns>
        public static SheetInfo ParseUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return null;
            }
            
            var sheetInfo = new SheetInfo();
            
            // Sheet IDの抽出
            var sheetIdMatch = Regex.Match(url, SHEET_ID_PATTERN);
            if (sheetIdMatch.Success && sheetIdMatch.Groups.Count > 1)
            {
                sheetInfo.SheetId = sheetIdMatch.Groups[1].Value;
            }
            else
            {
                Debug.LogWarning($"Sheet IDが見つかりません: {url}");
                return null;
            }
            
            // GIDの抽出
            var gidMatch = Regex.Match(url, GID_PATTERN);
            if (gidMatch.Success && gidMatch.Groups.Count > 1)
            {
                sheetInfo.Gid = gidMatch.Groups[1].Value;
            }
            else
            {
                // GIDが見つからない場合は0（最初のシート）とする
                sheetInfo.Gid = "0";
                Debug.Log($"GIDが見つからないため、0（最初のシート）を使用します: {url}");
            }
            
            return sheetInfo;
        }
        
        /// <summary>
        /// URLが有効なGoogle SpreadsheetsのURLかチェック
        /// </summary>
        public static bool IsValidGoogleSheetsUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return false;
            
            return url.Contains("docs.google.com/spreadsheets/") && 
                   Regex.IsMatch(url, SHEET_ID_PATTERN);
        }
        
        /// <summary>
        /// ConvertSettingにURL情報を適用
        /// </summary>
        public static bool ApplyUrlToConvertSetting(ConvertSetting setting, string url)
        {
            var sheetInfo = ParseUrl(url);
            if (sheetInfo != null && sheetInfo.IsValid)
            {
                setting.sheetID = sheetInfo.SheetId;
                setting.gid = sheetInfo.Gid;
                return true;
            }
            return false;
        }
    }
}