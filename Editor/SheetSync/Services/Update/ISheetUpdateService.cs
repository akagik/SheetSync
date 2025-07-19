using System.Collections.Generic;
using System.Threading.Tasks;

namespace SheetSync.Services.Update
{
    /// <summary>
    /// Google Sheets更新サービスのインターフェース
    /// </summary>
    public interface ISheetUpdateService
    {
        /// <summary>
        /// スプレッドシートの特定の行を更新
        /// </summary>
        /// <param name="spreadsheetId">スプレッドシートID</param>
        /// <param name="sheetName">シート名</param>
        /// <param name="keyColumn">キー列名</param>
        /// <param name="keyValue">キー値</param>
        /// <param name="updateData">更新データ（列名と値のペア）</param>
        /// <returns>更新が成功したかどうか</returns>
        Task<bool> UpdateRowAsync(
            string spreadsheetId, 
            string sheetName, 
            string keyColumn, 
            string keyValue, 
            Dictionary<string, object> updateData);
        
        /// <summary>
        /// 複数行を一括更新
        /// </summary>
        /// <param name="spreadsheetId">スプレッドシートID</param>
        /// <param name="sheetName">シート名</param>
        /// <param name="keyColumn">キー列名</param>
        /// <param name="updates">キー値と更新データのマップ</param>
        /// <returns>更新が成功したかどうか</returns>
        Task<bool> UpdateMultipleRowsAsync(
            string spreadsheetId,
            string sheetName,
            string keyColumn,
            Dictionary<string, Dictionary<string, object>> updates);
    }
}