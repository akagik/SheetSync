using KoheiUtils;

namespace SheetSync.Services
{
    /// <summary>
    /// コード生成サービスのインターフェース
    /// </summary>
    public interface ICodeGenerationService
    {
        /// <summary>
        /// 設定に基づいてコードを生成する
        /// </summary>
        void GenerateCode(ConvertSetting setting, GlobalCCSettings globalSettings);

        /// <summary>
        /// CSVデータからコードを生成する
        /// </summary>
        void GenerateCodeFromData(ConvertSetting setting, GlobalCCSettings globalSettings, ICsvData csvData, string directoryPath);

        /// <summary>
        /// アセットを作成する
        /// </summary>
        object CreateAssets(ConvertSetting setting, GlobalCCSettings globalSettings);

        /// <summary>
        /// データプロバイダーを使用してアセットを作成する
        /// </summary>
        object CreateAssets(ConvertSetting setting, GlobalCCSettings globalSettings, ICsvDataProvider dataProvider);
    }
}