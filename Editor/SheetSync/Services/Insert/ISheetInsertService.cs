using System.Collections.Generic;
using System.Threading.Tasks;

namespace SheetSync.Services.Insert
{
    /// <summary>
    /// Google Spreadsheetへの行挿入サービスのインターフェース
    /// </summary>
    public interface ISheetInsertService
    {
        /// <summary>
        /// スプレッドシートに単一行を挿入
        /// </summary>
        /// <param name="spreadsheetId">スプレッドシートID</param>
        /// <param name="sheetName">シート名</param>
        /// <param name="rowIndex">挿入位置（0ベース、0=1行目、1=2行目、2=3行目）</param>
        /// <param name="rowData">挿入データ（列名と値のペア）</param>
        /// <param name="verbose">詳細ログを出力するか</param>
        /// <returns>挿入が成功したかどうか</returns>
        Task<bool> InsertRowAsync(
            string spreadsheetId, 
            string sheetName, 
            int rowIndex,
            Dictionary<string, object> rowData,
            bool verbose = true);

        /// <summary>
        /// スプレッドシートに複数行を一括挿入
        /// 注意: 挿入は降順で実行され、行番号のずれを防ぎます
        /// </summary>
        /// <param name="spreadsheetId">スプレッドシートID</param>
        /// <param name="sheetName">シート名</param>
        /// <param name="insertions">挿入データのリスト（行番号と行データのペア）</param>
        /// <param name="verbose">詳細ログを出力するか</param>
        /// <returns>挿入が成功したかどうか</returns>
        Task<bool> InsertMultipleRowsAsync(
            string spreadsheetId,
            string sheetName,
            List<(int rowIndex, Dictionary<string, object> rowData)> insertions,
            bool verbose = true);
    }
}