using System.IO;
using UnityEngine;

namespace SheetSync
{
    /// <summary>
    /// ファイルベースの CSV データプロバイダー
    /// 既存の CSV ファイル読み込み処理をラップ
    /// </summary>
    public class FileCsvDataProvider : ICsvDataProvider
    {
        private readonly string _filePath;
        private readonly Models.GlobalCCSettings _globalSettings;
        
        public FileCsvDataProvider(string filePath, Models.GlobalCCSettings globalSettings)
        {
            _filePath = filePath;
            _globalSettings = globalSettings;
        }
        
        public ICsvData GetCsvData()
        {
            if (!IsAvailable())
            {
                Debug.LogError($"CSV ファイルが見つかりません: {_filePath}");
                return new CsvData();
            }
            
            // 既存の CsvLogic を使用してファイルを読み込む
            string csvContent = File.ReadAllText(_filePath);
            var csvData = CsvLogic.GetValidCsvData(csvContent, _globalSettings);
            
            return csvData;
        }
        
        public bool IsAvailable()
        {
            return File.Exists(_filePath);
        }
    }
}
