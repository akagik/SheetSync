using System.Collections.Generic;
using UnityEngine;

namespace SheetSync
{
    /// <summary>
    /// Google Sheets API レスポンスから直接データを提供するプロバイダー
    /// ファイル保存をバイパスして、メモリ効率的に処理
    /// </summary>
    public class GoogleSheetsCsvDataProvider : ICsvDataProvider
    {
        private readonly IList<IList<object>> _values;
        
        public GoogleSheetsCsvDataProvider(IList<IList<object>> values)
        {
            _values = values;
        }
        
        public ICsvData GetCsvData()
        {
            if (!IsAvailable())
            {
                Debug.LogError("Google Sheets データが null または空です");
                return new CsvData();
            }
            
            // SheetData を使用してメモリコピーなしでデータを提供
            return new SheetData(_values);
        }
        
        public bool IsAvailable()
        {
            return _values != null && _values.Count > 0;
        }
    }
}
