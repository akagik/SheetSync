using System;
using System.Collections.Generic;
using System.Linq;

namespace SheetSync
{
    /// <summary>
    /// 拡張されたSheetDataクラス
    /// 編集機能、インデックス機能、変更追跡機能を持つ
    /// </summary>
    public class ExtendedSheetData : SheetData
    {
        #region 内部クラス
        
        /// <summary>
        /// 変更の種類
        /// </summary>
        public enum ChangeType
        {
            Update,
            Insert,
            Delete
        }
        
        /// <summary>
        /// 変更記録
        /// </summary>
        public class ChangeRecord
        {
            public ChangeType Type { get; set; }
            public int RowIndex { get; set; }
            public string ColumnName { get; set; }
            public object OldValue { get; set; }
            public object NewValue { get; set; }
            public Dictionary<string, object> RowData { get; set; }
            public DateTime Timestamp { get; set; }
            
            public ChangeRecord()
            {
                Timestamp = DateTime.Now;
            }
        }
        
        #endregion
        
        private readonly IList<IList<object>> _originalValues;
        private IList<IList<object>> _editedValues;
        private readonly List<ChangeRecord> _changes = new List<ChangeRecord>();
        private Dictionary<string, int> _headerNameToColumnIndex;
        private readonly Dictionary<string, Dictionary<object, List<int>>> _keyIndices = new Dictionary<string, Dictionary<object, List<int>>>();
        
        /// <summary>
        /// 編集されたデータ（読み取り専用）
        /// </summary>
        public IList<IList<object>> EditedValues => _editedValues ?? _originalValues;
        
        /// <summary>
        /// 変更記録（読み取り専用）
        /// </summary>
        public IReadOnlyList<ChangeRecord> Changes => _changes.AsReadOnly();
        
        /// <summary>
        /// 変更があるかどうか
        /// </summary>
        public bool HasChanges => _changes.Count > 0;
        
        /// <summary>
        /// コンストラクタ
        /// </summary>
        public ExtendedSheetData(IList<IList<object>> values) : base(values)
        {
            _originalValues = values;
            BuildHeaderIndex();
        }
        
        #region ヘッダーインデックス機能
        
        /// <summary>
        /// ヘッダー行からカラムインデックスの辞書を構築
        /// </summary>
        private void BuildHeaderIndex()
        {
            _headerNameToColumnIndex = new Dictionary<string, int>();
            
            if (_originalValues.Count > 0 && _originalValues[0] != null)
            {
                var headers = _originalValues[0];
                for (int i = 0; i < headers.Count; i++)
                {
                    var headerName = headers[i]?.ToString();
                    if (!string.IsNullOrEmpty(headerName))
                    {
                        _headerNameToColumnIndex[headerName] = i;
                    }
                }
            }
        }
        
        /// <summary>
        /// ヘッダー名から列インデックスを取得
        /// </summary>
        public int GetColumnIndex(string headerName)
        {
            if (_headerNameToColumnIndex.TryGetValue(headerName, out int index))
            {
                return index;
            }
            return -1;
        }
        
        /// <summary>
        /// ヘッダー名から列インデックスへの辞書を取得
        /// </summary>
        public IReadOnlyDictionary<string, int> HeaderNameToColumnIndex => _headerNameToColumnIndex;
        
        #endregion
        
        #region キーインデックス機能
        
        /// <summary>
        /// 指定されたキー列でインデックスを作成
        /// </summary>
        public void BuildKeyIndex(string keyColumnName)
        {
            var columnIndex = GetColumnIndex(keyColumnName);
            if (columnIndex < 0)
            {
                throw new ArgumentException($"Column '{keyColumnName}' not found in headers");
            }
            
            var keyIndex = new Dictionary<object, List<int>>();
            var values = EditedValues;
            
            // ヘッダー行をスキップして、データ行を処理
            for (int i = 1; i < values.Count; i++)
            {
                if (values[i] != null && columnIndex < values[i].Count)
                {
                    var keyValue = values[i][columnIndex];
                    if (keyValue != null)
                    {
                        if (!keyIndex.ContainsKey(keyValue))
                        {
                            keyIndex[keyValue] = new List<int>();
                        }
                        keyIndex[keyValue].Add(i);
                    }
                }
            }
            
            _keyIndices[keyColumnName] = keyIndex;
        }
        
        /// <summary>
        /// キー値から行インデックスを取得
        /// </summary>
        public int GetRowIndex(string keyColumnName, string value)
        {
            if (!_keyIndices.ContainsKey(keyColumnName))
            {
                BuildKeyIndex(keyColumnName);
            }
            
            if (_keyIndices[keyColumnName].TryGetValue(value, out var indices) && indices.Count > 0)
            {
                return indices[0]; // 最初に見つかった行を返す
            }
            
            return -1;
        }
        
        /// <summary>
        /// キー値から複数の行インデックスを取得
        /// </summary>
        public List<int> GetRowIndices(string keyColumnName, string value)
        {
            if (!_keyIndices.ContainsKey(keyColumnName))
            {
                BuildKeyIndex(keyColumnName);
            }
            
            if (_keyIndices[keyColumnName].TryGetValue(value, out var indices))
            {
                return new List<int>(indices);
            }
            
            return new List<int>();
        }
        
        #endregion
        
        #region 編集機能
        
        /// <summary>
        /// コピーオンライトを確実に行う
        /// </summary>
        private void EnsureEditedValues()
        {
            if (_editedValues == null)
            {
                _editedValues = new List<IList<object>>();
                foreach (var row in _originalValues)
                {
                    if (row != null)
                    {
                        _editedValues.Add(new List<object>(row));
                    }
                    else
                    {
                        _editedValues.Add(null);
                    }
                }
            }
        }
        
        /// <summary>
        /// 特定のセルを更新
        /// </summary>
        public void UpdateCell(int rowIndex, string columnName, object newValue)
        {
            var columnIndex = GetColumnIndex(columnName);
            if (columnIndex < 0)
            {
                throw new ArgumentException($"Column '{columnName}' not found");
            }
            
            UpdateCell(rowIndex, columnIndex, newValue);
        }
        
        /// <summary>
        /// 特定のセルを更新（列インデックス版）
        /// </summary>
        public void UpdateCell(int rowIndex, int columnIndex, object newValue)
        {
            EnsureEditedValues();
            
            if (rowIndex < 0 || rowIndex >= _editedValues.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(rowIndex));
            }
            
            if (_editedValues[rowIndex] == null || columnIndex >= _editedValues[rowIndex].Count)
            {
                throw new ArgumentOutOfRangeException(nameof(columnIndex));
            }
            
            var oldValue = _editedValues[rowIndex][columnIndex];
            _editedValues[rowIndex][columnIndex] = newValue;
            
            // 変更を記録
            var columnName = GetColumnName(columnIndex);
            _changes.Add(new ChangeRecord
            {
                Type = ChangeType.Update,
                RowIndex = rowIndex,
                ColumnName = columnName,
                OldValue = oldValue,
                NewValue = newValue
            });
            
            // キーインデックスの更新が必要な場合
            UpdateKeyIndicesForCellChange(rowIndex, columnIndex, oldValue, newValue);
        }
        
        /// <summary>
        /// キー値を使用して行を更新
        /// </summary>
        public void UpdateRowByKey(string keyColumnName, string keyValue, Dictionary<string, object> updates)
        {
            var rowIndex = GetRowIndex(keyColumnName, keyValue);
            if (rowIndex < 0)
            {
                throw new ArgumentException($"Row with {keyColumnName}='{keyValue}' not found");
            }
            
            UpdateRow(rowIndex, updates);
        }
        
        /// <summary>
        /// 行を更新
        /// </summary>
        public void UpdateRow(int rowIndex, Dictionary<string, object> updates)
        {
            foreach (var kvp in updates)
            {
                UpdateCell(rowIndex, kvp.Key, kvp.Value);
            }
        }
        
        /// <summary>
        /// 行を挿入
        /// </summary>
        public void InsertRow(int rowIndex, Dictionary<string, object> rowData)
        {
            EnsureEditedValues();
            
            if (rowIndex < 0 || rowIndex > _editedValues.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(rowIndex));
            }
            
            // 新しい行を作成
            var newRow = new List<object>();
            
            // ヘッダーに基づいて行を構築
            if (_editedValues.Count > 0 && _editedValues[0] != null)
            {
                for (int i = 0; i < _editedValues[0].Count; i++)
                {
                    newRow.Add(null);
                }
                
                // データを設定
                foreach (var kvp in rowData)
                {
                    var columnIndex = GetColumnIndex(kvp.Key);
                    if (columnIndex >= 0 && columnIndex < newRow.Count)
                    {
                        newRow[columnIndex] = kvp.Value;
                    }
                }
            }
            
            // 行を挿入
            _editedValues.Insert(rowIndex, newRow);
            
            // 変更を記録
            _changes.Add(new ChangeRecord
            {
                Type = ChangeType.Insert,
                RowIndex = rowIndex,
                RowData = new Dictionary<string, object>(rowData)
            });
            
            // キーインデックスの更新
            UpdateKeyIndicesForInsert(rowIndex);
        }
        
        /// <summary>
        /// キー値を使用して行を削除
        /// </summary>
        public void DeleteRowByKey(string keyColumnName, string keyValue)
        {
            var rowIndex = GetRowIndex(keyColumnName, keyValue);
            if (rowIndex < 0)
            {
                throw new ArgumentException($"Row with {keyColumnName}='{keyValue}' not found");
            }
            
            DeleteRow(rowIndex);
        }
        
        /// <summary>
        /// 行を削除
        /// </summary>
        public void DeleteRow(int rowIndex)
        {
            EnsureEditedValues();
            
            if (rowIndex < 0 || rowIndex >= _editedValues.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(rowIndex));
            }
            
            // 削除前の行データを保存
            var deletedRowData = new Dictionary<string, object>();
            if (_editedValues[rowIndex] != null)
            {
                for (int i = 0; i < _editedValues[rowIndex].Count; i++)
                {
                    var columnName = GetColumnName(i);
                    if (!string.IsNullOrEmpty(columnName))
                    {
                        deletedRowData[columnName] = _editedValues[rowIndex][i];
                    }
                }
            }
            
            // 行を削除
            _editedValues.RemoveAt(rowIndex);
            
            // 変更を記録
            _changes.Add(new ChangeRecord
            {
                Type = ChangeType.Delete,
                RowIndex = rowIndex,
                RowData = deletedRowData
            });
            
            // キーインデックスの更新
            UpdateKeyIndicesForDelete(rowIndex);
        }
        
        #endregion
        
        #region ヘルパーメソッド
        
        /// <summary>
        /// 列インデックスから列名を取得
        /// </summary>
        private string GetColumnName(int columnIndex)
        {
            foreach (var kvp in _headerNameToColumnIndex)
            {
                if (kvp.Value == columnIndex)
                {
                    return kvp.Key;
                }
            }
            return $"Column{columnIndex}";
        }
        
        /// <summary>
        /// セル変更時のキーインデックス更新
        /// </summary>
        private void UpdateKeyIndicesForCellChange(int rowIndex, int columnIndex, object oldValue, object newValue)
        {
            foreach (var kvp in _keyIndices)
            {
                if (GetColumnIndex(kvp.Key) == columnIndex)
                {
                    // 古い値から行インデックスを削除
                    if (oldValue != null && kvp.Value.ContainsKey(oldValue))
                    {
                        kvp.Value[oldValue].Remove(rowIndex);
                        if (kvp.Value[oldValue].Count == 0)
                        {
                            kvp.Value.Remove(oldValue);
                        }
                    }
                    
                    // 新しい値に行インデックスを追加
                    if (newValue != null)
                    {
                        if (!kvp.Value.ContainsKey(newValue))
                        {
                            kvp.Value[newValue] = new List<int>();
                        }
                        kvp.Value[newValue].Add(rowIndex);
                    }
                }
            }
        }
        
        /// <summary>
        /// 行挿入時のキーインデックス更新
        /// </summary>
        private void UpdateKeyIndicesForInsert(int insertedRowIndex)
        {
            foreach (var keyIndex in _keyIndices.Values)
            {
                var updatedIndex = new Dictionary<object, List<int>>();
                
                foreach (var kvp in keyIndex)
                {
                    var newIndices = new List<int>();
                    foreach (var index in kvp.Value)
                    {
                        if (index >= insertedRowIndex)
                        {
                            newIndices.Add(index + 1);
                        }
                        else
                        {
                            newIndices.Add(index);
                        }
                    }
                    updatedIndex[kvp.Key] = newIndices;
                }
                
                keyIndex.Clear();
                foreach (var kvp in updatedIndex)
                {
                    keyIndex[kvp.Key] = kvp.Value;
                }
            }
        }
        
        /// <summary>
        /// 行削除時のキーインデックス更新
        /// </summary>
        private void UpdateKeyIndicesForDelete(int deletedRowIndex)
        {
            foreach (var keyIndex in _keyIndices.Values)
            {
                var updatedIndex = new Dictionary<object, List<int>>();
                
                foreach (var kvp in keyIndex)
                {
                    var newIndices = new List<int>();
                    foreach (var index in kvp.Value)
                    {
                        if (index > deletedRowIndex)
                        {
                            newIndices.Add(index - 1);
                        }
                        else if (index < deletedRowIndex)
                        {
                            newIndices.Add(index);
                        }
                        // deletedRowIndex と同じインデックスは追加しない
                    }
                    
                    if (newIndices.Count > 0)
                    {
                        updatedIndex[kvp.Key] = newIndices;
                    }
                }
                
                keyIndex.Clear();
                foreach (var kvp in updatedIndex)
                {
                    keyIndex[kvp.Key] = kvp.Value;
                }
            }
        }
        
        /// <summary>
        /// 変更をクリア
        /// </summary>
        public void ClearChanges()
        {
            _changes.Clear();
        }
        
        /// <summary>
        /// 編集をリセット（元のデータに戻す）
        /// </summary>
        public void ResetEdits()
        {
            _editedValues = null;
            _changes.Clear();
            _keyIndices.Clear();
            BuildHeaderIndex();
        }
        
        #endregion
    }
}