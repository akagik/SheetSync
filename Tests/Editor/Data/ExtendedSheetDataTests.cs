using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using SheetSync;

namespace Kohei.SheetSync.Tests.Editor.Data
{
    /// <summary>
    /// ExtendedSheetDataのテストクラス
    /// </summary>
    [TestFixture]
    public class ExtendedSheetDataTests
    {
        private ExtendedSheetData _sheetData;
        private IList<IList<object>> _testValues;
        
        [SetUp]
        public void Setup()
        {
            // テスト用データの準備
            _testValues = new List<IList<object>>
            {
                new List<object> { "ID", "Name", "Age", "Email" },          // ヘッダー行
                new List<object> { "1", "Alice", "25", "alice@example.com" },
                new List<object> { "2", "Bob", "30", "bob@example.com" },
                new List<object> { "3", "Charlie", "35", "charlie@example.com" }
            };
            
            _sheetData = new ExtendedSheetData(_testValues);
        }
        
        #region ヘッダーインデックス機能のテスト
        
        [Test]
        public void GetColumnIndex_ValidHeaderName_ReturnsCorrectIndex()
        {
            // Arrange & Act
            var idIndex = _sheetData.GetColumnIndex("ID");
            var nameIndex = _sheetData.GetColumnIndex("Name");
            var ageIndex = _sheetData.GetColumnIndex("Age");
            var emailIndex = _sheetData.GetColumnIndex("Email");
            
            // Assert
            Assert.AreEqual(0, idIndex);
            Assert.AreEqual(1, nameIndex);
            Assert.AreEqual(2, ageIndex);
            Assert.AreEqual(3, emailIndex);
        }
        
        [Test]
        public void GetColumnIndex_InvalidHeaderName_ReturnsNegativeOne()
        {
            // Arrange & Act
            var index = _sheetData.GetColumnIndex("NonExistent");
            
            // Assert
            Assert.AreEqual(-1, index);
        }
        
        [Test]
        public void HeaderNameToColumnIndex_ReturnsAllHeaders()
        {
            // Arrange & Act
            var headerMap = _sheetData.HeaderNameToColumnIndex;
            
            // Assert
            Assert.AreEqual(4, headerMap.Count);
            Assert.IsTrue(headerMap.ContainsKey("ID"));
            Assert.IsTrue(headerMap.ContainsKey("Name"));
            Assert.IsTrue(headerMap.ContainsKey("Age"));
            Assert.IsTrue(headerMap.ContainsKey("Email"));
        }
        
        #endregion
        
        #region キーインデックス機能のテスト
        
        [Test]
        public void BuildKeyIndex_ValidColumn_CreatesIndex()
        {
            // Arrange & Act
            _sheetData.BuildKeyIndex("ID");
            
            // Assert - 内部でインデックスが作成されていることを確認
            var rowIndex1 = _sheetData.GetRowIndex("ID", "1");
            var rowIndex2 = _sheetData.GetRowIndex("ID", "2");
            var rowIndex3 = _sheetData.GetRowIndex("ID", "3");
            
            Assert.AreEqual(1, rowIndex1);
            Assert.AreEqual(2, rowIndex2);
            Assert.AreEqual(3, rowIndex3);
        }
        
        [Test]
        public void BuildKeyIndex_InvalidColumn_ThrowsException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentException>(() => _sheetData.BuildKeyIndex("NonExistent"));
        }
        
        [Test]
        public void GetRowIndex_WithoutBuildingIndex_BuildsAutomatically()
        {
            // Arrange & Act - インデックスを明示的に構築せずに使用
            var rowIndex = _sheetData.GetRowIndex("Name", "Bob");
            
            // Assert
            Assert.AreEqual(2, rowIndex);
        }
        
        [Test]
        public void GetRowIndex_NonExistentValue_ReturnsNegativeOne()
        {
            // Arrange & Act
            var rowIndex = _sheetData.GetRowIndex("Name", "NonExistent");
            
            // Assert
            Assert.AreEqual(-1, rowIndex);
        }
        
        [Test]
        public void GetRowIndices_DuplicateValues_ReturnsAllIndices()
        {
            // Arrange - 重複データを持つテストデータ
            var duplicateValues = new List<IList<object>>
            {
                new List<object> { "Category", "Value" },
                new List<object> { "A", "100" },
                new List<object> { "B", "200" },
                new List<object> { "A", "300" },  // 重複
                new List<object> { "A", "400" }   // 重複
            };
            var sheetData = new ExtendedSheetData(duplicateValues);
            
            // Act
            var indices = sheetData.GetRowIndices("Category", "A");
            
            // Assert
            Assert.AreEqual(3, indices.Count);
            Assert.Contains(1, indices);
            Assert.Contains(3, indices);
            Assert.Contains(4, indices);
        }
        
        #endregion
        
        #region 編集機能のテスト
        
        [Test]
        public void UpdateCell_ValidInput_UpdatesValue()
        {
            // Arrange
            var originalValue = _sheetData.GetCell(1, 1); // "Alice"
            
            // Act
            _sheetData.UpdateCell(1, "Name", "Alice Updated");
            
            // Assert
            Assert.AreEqual("Alice Updated", _sheetData.EditedValues[1][1]);
            Assert.AreEqual(originalValue, _testValues[1][1]); // 元のデータは変更されない
            Assert.IsTrue(_sheetData.HasChanges);
        }
        
        [Test]
        public void UpdateCell_InvalidColumn_ThrowsException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentException>(() => _sheetData.UpdateCell(1, "NonExistent", "Value"));
        }
        
        [Test]
        public void UpdateCell_InvalidRowIndex_ThrowsException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => _sheetData.UpdateCell(10, "Name", "Value"));
        }
        
        [Test]
        public void UpdateRowByKey_ValidKey_UpdatesRow()
        {
            // Arrange
            var updates = new Dictionary<string, object>
            {
                ["Name"] = "Bob Updated",
                ["Age"] = "31"
            };
            
            // Act
            _sheetData.UpdateRowByKey("ID", "2", updates);
            
            // Assert
            Assert.AreEqual("Bob Updated", _sheetData.EditedValues[2][1]);
            Assert.AreEqual("31", _sheetData.EditedValues[2][2]);
            Assert.AreEqual(2, _sheetData.Changes.Count); // 2つの変更
        }
        
        [Test]
        public void UpdateRowByKey_NonExistentKey_ThrowsException()
        {
            // Arrange
            var updates = new Dictionary<string, object> { ["Name"] = "Test" };
            
            // Act & Assert
            Assert.Throws<ArgumentException>(() => _sheetData.UpdateRowByKey("ID", "999", updates));
        }
        
        #endregion
        
        #region 行挿入機能のテスト
        
        [Test]
        public void InsertRow_ValidInput_InsertsNewRow()
        {
            // Arrange
            var newRowData = new Dictionary<string, object>
            {
                ["ID"] = "4",
                ["Name"] = "David",
                ["Age"] = "28",
                ["Email"] = "david@example.com"
            };
            
            // Act
            _sheetData.InsertRow(2, newRowData); // 3行目に挿入
            
            // Assert
            Assert.AreEqual(5, _sheetData.EditedValues.Count); // 元の4行 + 1行
            Assert.AreEqual("4", _sheetData.EditedValues[2][0]);
            Assert.AreEqual("David", _sheetData.EditedValues[2][1]);
            Assert.AreEqual("2", _sheetData.EditedValues[3][0]); // 元の2行目が3行目に移動
            Assert.IsTrue(_sheetData.HasChanges);
        }
        
        [Test]
        public void InsertRow_InvalidRowIndex_ThrowsException()
        {
            // Arrange
            var newRowData = new Dictionary<string, object> { ["ID"] = "5" };
            
            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => _sheetData.InsertRow(-1, newRowData));
            Assert.Throws<ArgumentOutOfRangeException>(() => _sheetData.InsertRow(10, newRowData));
        }
        
        [Test]
        public void InsertRow_UpdatesKeyIndices()
        {
            // Arrange
            _sheetData.BuildKeyIndex("ID");
            var newRowData = new Dictionary<string, object>
            {
                ["ID"] = "4",
                ["Name"] = "David"
            };
            
            // Act
            _sheetData.InsertRow(2, newRowData);
            
            // Assert - インデックスが正しく更新されているか確認
            Assert.AreEqual(1, _sheetData.GetRowIndex("ID", "1")); // 変更なし
            Assert.AreEqual(3, _sheetData.GetRowIndex("ID", "2")); // 2→3
            Assert.AreEqual(4, _sheetData.GetRowIndex("ID", "3")); // 3→4
            Assert.AreEqual(2, _sheetData.GetRowIndex("ID", "4")); // 新規挿入
        }
        
        #endregion
        
        #region 行削除機能のテスト
        
        [Test]
        public void DeleteRow_ValidIndex_DeletesRow()
        {
            // Arrange
            var originalCount = _sheetData.EditedValues.Count;
            
            // Act
            _sheetData.DeleteRow(2); // Bob を削除
            
            // Assert
            Assert.AreEqual(originalCount - 1, _sheetData.EditedValues.Count);
            Assert.AreEqual("3", _sheetData.EditedValues[2][0]); // Charlie が2行目に移動
            Assert.IsTrue(_sheetData.HasChanges);
        }
        
        [Test]
        public void DeleteRowByKey_ValidKey_DeletesRow()
        {
            // Arrange & Act
            _sheetData.DeleteRowByKey("Name", "Bob");
            
            // Assert
            Assert.AreEqual(3, _sheetData.EditedValues.Count);
            Assert.AreEqual(-1, _sheetData.GetRowIndex("Name", "Bob"));
        }
        
        [Test]
        public void DeleteRow_InvalidIndex_ThrowsException()
        {
            // Arrange & Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => _sheetData.DeleteRow(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => _sheetData.DeleteRow(10));
        }
        
        [Test]
        public void DeleteRow_UpdatesKeyIndices()
        {
            // Arrange
            _sheetData.BuildKeyIndex("ID");
            
            // Act
            _sheetData.DeleteRow(2); // ID=2 を削除
            
            // Assert
            Assert.AreEqual(1, _sheetData.GetRowIndex("ID", "1")); // 変更なし
            Assert.AreEqual(-1, _sheetData.GetRowIndex("ID", "2")); // 削除された
            Assert.AreEqual(2, _sheetData.GetRowIndex("ID", "3")); // 3→2
        }
        
        #endregion
        
        #region 変更追跡機能のテスト
        
        [Test]
        public void Changes_TrackAllOperations()
        {
            // Act - 様々な操作を実行
            _sheetData.UpdateCell(1, "Name", "Alice Updated");
            _sheetData.InsertRow(2, new Dictionary<string, object> { ["ID"] = "4", ["Name"] = "David" });
            _sheetData.DeleteRow(3);
            
            // Assert
            Assert.AreEqual(3, _sheetData.Changes.Count);
            
            var change1 = _sheetData.Changes[0];
            Assert.AreEqual(ExtendedSheetData.ChangeType.Update, change1.Type);
            Assert.AreEqual(1, change1.RowIndex);
            Assert.AreEqual("Name", change1.ColumnName);
            Assert.AreEqual("Alice", change1.OldValue);
            Assert.AreEqual("Alice Updated", change1.NewValue);
            
            var change2 = _sheetData.Changes[1];
            Assert.AreEqual(ExtendedSheetData.ChangeType.Insert, change2.Type);
            Assert.AreEqual(2, change2.RowIndex);
            Assert.IsNotNull(change2.RowData);
            
            var change3 = _sheetData.Changes[2];
            Assert.AreEqual(ExtendedSheetData.ChangeType.Delete, change3.Type);
            Assert.AreEqual(3, change3.RowIndex);
            Assert.IsNotNull(change3.RowData);
        }
        
        [Test]
        public void ClearChanges_RemovesAllChanges()
        {
            // Arrange
            _sheetData.UpdateCell(1, "Name", "Test");
            Assert.IsTrue(_sheetData.HasChanges);
            
            // Act
            _sheetData.ClearChanges();
            
            // Assert
            Assert.IsFalse(_sheetData.HasChanges);
            Assert.AreEqual(0, _sheetData.Changes.Count);
            // ただし編集されたデータは保持される
            Assert.AreEqual("Test", _sheetData.EditedValues[1][1]);
        }
        
        [Test]
        public void ResetEdits_RestoresOriginalData()
        {
            // Arrange
            _sheetData.UpdateCell(1, "Name", "Test");
            _sheetData.InsertRow(2, new Dictionary<string, object> { ["ID"] = "5" });
            
            // Act
            _sheetData.ResetEdits();
            
            // Assert
            Assert.IsFalse(_sheetData.HasChanges);
            Assert.AreEqual(4, _sheetData.EditedValues.Count); // 元の行数
            Assert.AreEqual("Alice", _sheetData.EditedValues[1][1]); // 元の値
        }
        
        #endregion
        
        #region コピーオンライト機能のテスト
        
        [Test]
        public void EditedValues_InitiallyReturnsOriginal()
        {
            // Arrange & Act
            var edited = _sheetData.EditedValues;
            
            // Assert
            Assert.AreSame(_testValues, edited); // 編集前は同じ参照
        }
        
        [Test]
        public void EditedValues_AfterEdit_ReturnsCopy()
        {
            // Arrange & Act
            _sheetData.UpdateCell(1, "Name", "Test");
            var edited = _sheetData.EditedValues;
            
            // Assert
            Assert.AreNotSame(_testValues, edited); // 編集後は別の参照
            Assert.AreEqual("Test", edited[1][1]);
            Assert.AreEqual("Alice", _testValues[1][1]); // 元のデータは変更されない
        }
        
        #endregion
        
        #region キーインデックス更新のテスト
        
        [Test]
        public void UpdateCell_UpdatesKeyIndex_WhenKeyColumnChanged()
        {
            // Arrange
            _sheetData.BuildKeyIndex("ID");
            
            // Act
            _sheetData.UpdateCell(2, "ID", "99"); // ID "2" → "99"
            
            // Assert
            Assert.AreEqual(-1, _sheetData.GetRowIndex("ID", "2")); // 古い値では見つからない
            Assert.AreEqual(2, _sheetData.GetRowIndex("ID", "99")); // 新しい値で見つかる
        }
        
        [Test]
        public void InsertRow_AddsToKeyIndex()
        {
            // Arrange
            _sheetData.BuildKeyIndex("Name");
            
            // Act
            _sheetData.InsertRow(2, new Dictionary<string, object> { ["Name"] = "Eve" });
            
            // Assert
            Assert.AreEqual(2, _sheetData.GetRowIndex("Name", "Eve"));
        }
        
        #endregion
    }
}