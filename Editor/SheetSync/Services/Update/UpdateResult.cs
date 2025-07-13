using System;
using System.Collections.Generic;

namespace SheetSync.Services.Update
{
    /// <summary>
    /// 更新操作の結果を表すクラス
    /// </summary>
    public class UpdateResult
    {
        /// <summary>
        /// 更新が成功したかどうか
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// 更新された行数
        /// </summary>
        public int UpdatedRowCount { get; set; }
        
        /// <summary>
        /// エラーメッセージ（失敗時）
        /// </summary>
        public string ErrorMessage { get; set; }
        
        /// <summary>
        /// 更新された行の詳細情報
        /// </summary>
        public List<UpdatedRowInfo> UpdatedRows { get; set; } = new List<UpdatedRowInfo>();
        
        /// <summary>
        /// 処理時間（ミリ秒）
        /// </summary>
        public long ElapsedMilliseconds { get; set; }
    }
    
    /// <summary>
    /// 更新された行の詳細情報
    /// </summary>
    public class UpdatedRowInfo
    {
        /// <summary>
        /// スプレッドシート上の行番号（1始まり）
        /// </summary>
        public int RowNumber { get; set; }
        
        /// <summary>
        /// 更新されたフィールドと値の変更
        /// </summary>
        public Dictionary<string, FieldChange> Changes { get; set; } = new Dictionary<string, FieldChange>();
    }
    
    /// <summary>
    /// フィールドの変更情報
    /// </summary>
    public class FieldChange
    {
        /// <summary>
        /// 変更前の値
        /// </summary>
        public object OldValue { get; set; }
        
        /// <summary>
        /// 変更後の値
        /// </summary>
        public object NewValue { get; set; }
        
        /// <summary>
        /// 値の型
        /// </summary>
        public Type FieldType { get; set; }
    }
}