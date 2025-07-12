namespace SheetSync
{
    /// <summary>
    /// CSV データプロバイダーの抽象インターフェース
    /// ファイルや Google Sheets など、様々なソースからデータを提供
    /// </summary>
    public interface ICsvDataProvider
    {
        /// <summary>
        /// CSV データを取得する
        /// </summary>
        /// <returns>ICsvData インスタンス</returns>
        ICsvData GetCsvData();
        
        /// <summary>
        /// データソースが利用可能かどうかを確認
        /// </summary>
        /// <returns>利用可能な場合は true</returns>
        bool IsAvailable();
    }
}
