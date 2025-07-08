using UnityEngine;

namespace KoheiUtils
{
    /// <summary>
    /// CsvConverter のグローバルな設定.
    /// </summary>
    [CreateAssetMenu(menuName = "KoheiUtils/CsvConverter/GlobalSettings")]
    public class GlobalCCSettings : ScriptableObject
    {
        [Header("Google Spread Sheet")]
        public string apiKey;

        public bool useV4 = true;
            
        /// <summary>
        /// フィールド名の行の index.
        /// 1行目が index 0 に対応する.
        /// </summary>
        [Header("Csv Converter")]
        public int rowIndexOfName = 0;

        /// <summary>
        /// 型指定の行の index.
        /// </summary>
        public int rowIndexOfType = 1;

        /// <summary>
        /// 列が有効かどうかを指定する行の index.
        /// </summary>
        public int rowIndexOfEnabledColumn = -1;

        /// <summary>
        /// 実際のコンテンツ定義が始まる index.
        /// </summary>
        public int rowIndexOfContentStart = 2;

        /// <summary>
        /// enum 定義の実際のコンテンツ定義が始まる index.
        /// </summary>
        public int rowIndexOfEnumContentStart = 1;

        /// <summary>
        /// テーブルが始まる列 index.
        ///
        /// 例えば, これを 2 に設定していると index 0, 1 の列は無視される.
        /// </summary>
        public int columnIndexOfTableStart = 0;
        
        public string versionFieldName = "version";

        /// <summary>
        /// テーブルの終わりを示す EDN マーカー機能を有効にするかを指定する.
        /// </summary>
        public bool isEndMarkerEnabled = false;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.ShowIf("isEndMarkerEnabled")]
#endif
        public int columnIndexOfEndMarker = 0;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.ShowIf("isEndMarkerEnabled")]
#endif
        public string endMarker = "END";
        
        [Tooltip("csv ファイルのパスを指定しなかった場合のデフォルトのパス")]
        public string tempCsvPath = "/temp/temp.csv";
        
        [Tooltip("出力ログ")]
        public AssetsGenerator.ResultType logType = AssetsGenerator.ResultType.All;

        /// <summary>
        /// Str2Converter で利用される追加の型.
        /// 基本は t:TypeName フィルターで検索されるアセットが対象となる.
        /// </summary>
        public string[] customAssetTypes = new string[0];
    }
}