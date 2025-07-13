namespace SheetSync.Services.Update
{
    /// <summary>
    /// MVP実装用のシンプルな更新クエリ
    /// </summary>
    public class SimpleUpdateQuery<T> where T : class
    {
        /// <summary>
        /// 検索対象のフィールド名（例: "humanId"）
        /// </summary>
        public string FieldName { get; set; }
        
        /// <summary>
        /// 検索する値（例: 1）
        /// </summary>
        public object SearchValue { get; set; }
        
        /// <summary>
        /// 更新対象のフィールド名（例: "name"）
        /// </summary>
        public string UpdateFieldName { get; set; }
        
        /// <summary>
        /// 更新する値（例: "Tanaka"）
        /// </summary>
        public object UpdateValue { get; set; }
        
        /// <summary>
        /// 対象となる型
        /// </summary>
        public System.Type TargetType => typeof(T);
    }
}