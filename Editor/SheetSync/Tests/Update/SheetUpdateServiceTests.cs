using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using SheetSync.Services.Update;
using UnityEngine;
using UnityEngine.TestTools;

namespace SheetSync.Tests.Update
{
    /// <summary>
    /// SheetUpdateServiceのテストクラス
    /// </summary>
    public class SheetUpdateServiceTests
    {
        private MockSheetRepository _mockRepo;
        private TestableSheetUpdateService _service;
        
        [SetUp]
        public void Setup()
        {
            _mockRepo = new MockSheetRepository();
            _service = new TestableSheetUpdateService(_mockRepo);
        }
        
        [Test]
        public async Task UpdateSingleRow_ValidQuery_Success()
        {
            // Arrange
            _mockRepo.SetTestData(new List<IList<object>>
            {
                new List<object> { "humanId", "name", "age" },      // ヘッダー行
                new List<object> { "int", "string", "int" },        // 型定義行
                new List<object> { 0, "Kohei", 34 },
                new List<object> { 1, "Taro", 23 },
                new List<object> { 2, "Mami", 45 }
            });
            
            var query = new SimpleUpdateQuery<HumanMaster>
            {
                FieldName = "humanId",
                SearchValue = 1,
                UpdateFieldName = "name",
                UpdateValue = "Tanaka"
            };
            
            // Act
            var result = await _service.UpdateSingleRowAsync(query);
            
            // Assert
            Assert.IsTrue(result.Success);
            Assert.AreEqual(1, result.UpdatedRowCount);
            Assert.AreEqual(4, result.UpdatedRows[0].RowNumber); // 0始まりインデックス+1
            Assert.AreEqual("Taro", result.UpdatedRows[0].Changes["name"].OldValue);
            Assert.AreEqual("Tanaka", result.UpdatedRows[0].Changes["name"].NewValue);
        }
        
        [Test]
        public async Task UpdateSingleRow_RowNotFound_Failure()
        {
            // Arrange
            _mockRepo.SetTestData(new List<IList<object>>
            {
                new List<object> { "humanId", "name", "age" },
                new List<object> { "int", "string", "int" },
                new List<object> { 0, "Kohei", 34 }
            });
            
            var query = new SimpleUpdateQuery<HumanMaster>
            {
                FieldName = "humanId",
                SearchValue = 999, // 存在しないID
                UpdateFieldName = "name",
                UpdateValue = "Tanaka"
            };
            
            // Act
            var result = await _service.UpdateSingleRowAsync(query);
            
            // Assert
            Assert.IsFalse(result.Success);
            Assert.AreEqual(0, result.UpdatedRowCount);
            Assert.IsTrue(result.ErrorMessage.Contains("一致する行が見つかりません"));
        }
        
        [Test]
        public async Task UpdateSingleRow_InvalidField_Failure()
        {
            // Arrange
            _mockRepo.SetTestData(new List<IList<object>>
            {
                new List<object> { "humanId", "name", "age" },
                new List<object> { "int", "string", "int" },
                new List<object> { 1, "Taro", 23 }
            });
            
            var query = new SimpleUpdateQuery<HumanMaster>
            {
                FieldName = "invalidField", // 存在しないフィールド
                SearchValue = 1,
                UpdateFieldName = "name",
                UpdateValue = "Tanaka"
            };
            
            // Act
            var result = await _service.UpdateSingleRowAsync(query);
            
            // Assert
            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.ErrorMessage.Contains("フィールド 'invalidField' が見つかりません"));
        }
        
        [Test]
        public void Constructor_NoApiKey_ThrowsException()
        {
            // Arrange
            UnityEditor.EditorPrefs.DeleteKey("SheetSync_ApiKey");
            var setting = ScriptableObject.CreateInstance<ConvertSetting>();
            
            // Act & Assert
            Assert.Throws<InvalidOperationException>(() =>
            {
                new SheetUpdateService(setting);
            });
        }
    }
    
    /// <summary>
    /// テスト用のモックリポジトリ
    /// </summary>
    public class MockSheetRepository
    {
        private List<IList<object>> _testData;
        
        public void SetTestData(List<IList<object>> data)
        {
            _testData = data;
        }
        
        public List<IList<object>> GetData()
        {
            return _testData;
        }
        
        public void UpdateCell(int row, int col, object value)
        {
            if (_testData != null && row < _testData.Count && col < _testData[row].Count)
            {
                _testData[row][col] = value;
            }
        }
    }
    
    /// <summary>
    /// テスト可能なSheetUpdateService
    /// </summary>
    public class TestableSheetUpdateService : SheetUpdateService
    {
        private readonly MockSheetRepository _mockRepo;
        
        public TestableSheetUpdateService(MockSheetRepository mockRepo) 
            : base(CreateTestSetting())
        {
            _mockRepo = mockRepo;
        }
        
        private static ConvertSetting CreateTestSetting()
        {
            // テスト用のAPIキーを設定
            UnityEditor.EditorPrefs.SetString("SheetSync_ApiKey", "test-api-key");
            
            var setting = ScriptableObject.CreateInstance<ConvertSetting>();
            setting.useGSPlugin = true;
            setting.sheetID = "test-sheet-id";
            setting.gid = "0";
            return setting;
        }
        
        // テスト用にGetSheetDataAsyncをオーバーライド
        protected override async Task<Google.Apis.Sheets.v4.Data.ValueRange> GetSheetDataAsync()
        {
            await Task.Yield(); // 非同期をシミュレート
            
            return new Google.Apis.Sheets.v4.Data.ValueRange
            {
                Values = _mockRepo.GetData()
            };
        }
    }
    
    /// <summary>
    /// テスト用のHumanMasterクラス
    /// </summary>
    [Serializable]
    public class HumanMaster
    {
        public int humanId;
        public string name;
        public int age;
    }
}