using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using SheetSync.Services.Insert;
using SheetSync.Services.Auth;
using System.IO;

namespace Kohei.SheetSync.Tests.Editor.Insert
{
    /// <summary>
    /// SheetInsertServiceAccountServiceのテストクラス
    /// テスト用スプレッドシート: https://docs.google.com/spreadsheets/d/1eDSiCuI_HLeCV96rZioy_PD85AbNmmqdzrxjjS7sJ_w/edit?gid=1380898534#gid=1380898534
    /// 
    /// 注意: 
    /// 1. [IntegrationTest]属性が付いたテストは実際のGoogle Sheets APIを使用します
    /// 2. サービスアカウントキーが正しく設定されている必要があります
    /// 3. テスト用スプレッドシートへのアクセス権限が必要です
    /// 4. 挿入テストは元のデータを変更する可能性があります
    /// </summary>
    [TestFixture]
    public class SheetInsertServiceAccountServiceTests
    {
        private const string TEST_SPREADSHEET_ID = "1eDSiCuI_HLeCV96rZioy_PD85AbNmmqdzrxjjS7sJ_w";
        private const string TEST_SHEET_NAME = "Human";
        private const int TEST_SHEET_ID = 1380898534;
        
        private SheetInsertServiceAccountService _service;
        private bool _skipIntegrationTests = false;
        
        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            // サービスアカウントキーが存在するかチェック
            var keyPath = Path.Combine(Application.dataPath, "..", "ProjectSettings", "SheetSync", "service-account-key.json");
            if (!File.Exists(keyPath))
            {
                _skipIntegrationTests = true;
                Debug.LogWarning("サービスアカウントキーが見つかりません。統合テストはスキップされます。");
            }
        }
        
        [SetUp]
        public void Setup()
        {
            _service = new SheetInsertServiceAccountService();
        }
        
        [TearDown]
        public void TearDown()
        {
            // 静的フィールドをクリア
            GoogleServiceAccountAuth.ClearAuthentication();
        }
        
        #region 単体テスト（APIを使用しない）
        
        [Test]
        public async Task InsertRowAsync_NotAuthenticated_ReturnsFalse()
        {
            // Arrange
            // 認証されていない状態を確保
            GoogleServiceAccountAuth.ClearAuthentication();
            
            var rowData = new Dictionary<string, object> 
            { 
                ["humanId"] = "100",
                ["name"] = "Test",
                ["age"] = 30
            };
            
            // Act
            var result = await _service.InsertRowAsync(
                TEST_SPREADSHEET_ID,
                TEST_SHEET_NAME,
                2, // ヘッダー後の位置
                rowData
            );
            
            // Assert
            Assert.IsFalse(result);
            LogAssert.Expect(LogType.Error, "サービスアカウント認証が必要です。");
        }
        
        [Test]
        public async Task InsertMultipleRowsAsync_InvalidRowIndex_ReturnsFalse()
        {
            // Arrange
            GoogleServiceAccountAuth.ClearAuthentication();
            
            var insertions = new List<(int rowIndex, Dictionary<string, object> rowData)>
            {
                (-1, new Dictionary<string, object> { ["name"] = "Invalid" }) // 無効な行番号
            };
            
            // Act
            var result = await _service.InsertMultipleRowsAsync(
                TEST_SPREADSHEET_ID,
                TEST_SHEET_NAME,
                insertions
            );
            
            // Assert
            Assert.IsFalse(result);
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("無効な行番号"));
        }
        
        #endregion
        
        #region 統合テスト（実際のAPIを使用）
        
        /// <summary>
        /// 統合テスト用のカスタム属性
        /// </summary>
        public class IntegrationTestAttribute : CategoryAttribute
        {
            public IntegrationTestAttribute() : base("Integration") { }
        }
        
        [Test]
        [IntegrationTest]
        public async Task InsertRowAsync_ValidData_InsertsSuccessfully()
        {
            if (_skipIntegrationTests)
            {
                Assert.Ignore("サービスアカウントキーが設定されていません");
                return;
            }
            
            // Arrange
            await AuthenticateServiceAccount();
            
            var timestamp = DateTime.Now.Ticks;
            var rowData = new Dictionary<string, object>
            {
                ["humanId"] = "999",
                ["name"] = $"TestInsert_{timestamp}",
                ["age"] = 99,
                ["備考"] = "テスト挿入データ"
            };
            
            // Act
            var result = await _service.InsertRowAsync(
                TEST_SPREADSHEET_ID,
                TEST_SHEET_NAME,
                2, // 3番目の位置（0ベース、ヘッダー後）
                rowData
            );
            
            // Assert
            Assert.IsTrue(result);
            LogAssert.Expect(LogType.Log, "挿入成功: 行を挿入しました。");
            
            // 注意: 実際のスプレッドシートにデータが挿入されます
            // 必要に応じて手動で削除してください
        }
        
        [Test]
        [IntegrationTest]
        public async Task InsertRowAsync_EmptyData_InsertsEmptyRow()
        {
            if (_skipIntegrationTests)
            {
                Assert.Ignore("サービスアカウントキーが設定されていません");
                return;
            }
            
            // Arrange
            await AuthenticateServiceAccount();
            
            var rowData = new Dictionary<string, object>(); // 空のデータ
            
            // Act
            var result = await _service.InsertRowAsync(
                TEST_SPREADSHEET_ID,
                TEST_SHEET_NAME,
                1,
                rowData
            );
            
            // Assert
            Assert.IsFalse(result); // 空のデータは失敗扱い
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("有効なデータがありません"));
        }
        
        [Test]
        [IntegrationTest]
        public async Task InsertMultipleRowsAsync_ValidData_InsertsAllRows()
        {
            if (_skipIntegrationTests)
            {
                Assert.Ignore("サービスアカウントキーが設定されていません");
                return;
            }
            
            // Arrange
            await AuthenticateServiceAccount();
            
            var timestamp = DateTime.Now.ToString("HHmmss");
            var insertions = new List<(int rowIndex, Dictionary<string, object> rowData)>
            {
                (1, new Dictionary<string, object> 
                { 
                    ["humanId"] = "901",
                    ["name"] = $"BatchInsert1_{timestamp}",
                    ["age"] = 20
                }),
                (3, new Dictionary<string, object> 
                { 
                    ["humanId"] = "902",
                    ["name"] = $"BatchInsert2_{timestamp}",
                    ["age"] = 21
                }),
                (5, new Dictionary<string, object> 
                { 
                    ["humanId"] = "903",
                    ["name"] = $"BatchInsert3_{timestamp}",
                    ["age"] = 22
                })
            };
            
            // Act
            var result = await _service.InsertMultipleRowsAsync(
                TEST_SPREADSHEET_ID,
                TEST_SHEET_NAME,
                insertions
            );
            
            // Assert
            Assert.IsTrue(result);
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex("一括挿入完了: 成功=3, 失敗=0"));
        }
        
        [Test]
        [IntegrationTest]
        public async Task InsertMultipleRowsAsync_MixedValidInvalid_PartialSuccess()
        {
            if (_skipIntegrationTests)
            {
                Assert.Ignore("サービスアカウントキーが設定されていません");
                return;
            }
            
            // Arrange
            await AuthenticateServiceAccount();
            
            var timestamp = DateTime.Now.Ticks;
            var insertions = new List<(int rowIndex, Dictionary<string, object> rowData)>
            {
                (1, new Dictionary<string, object> 
                { 
                    ["humanId"] = "801",
                    ["name"] = $"ValidInsert_{timestamp}"
                }),
                (1000, new Dictionary<string, object> // 範囲外の行番号
                { 
                    ["humanId"] = "802",
                    ["name"] = "OutOfRange"
                }),
                (2, new Dictionary<string, object>()) // 空のデータ
            };
            
            // Act
            var result = await _service.InsertMultipleRowsAsync(
                TEST_SPREADSHEET_ID,
                TEST_SHEET_NAME,
                insertions,
                verbose: false
            );
            
            // Assert
            Assert.IsFalse(result); // 一部失敗があるのでfalse
        }
        
        [Test]
        [IntegrationTest]
        public async Task InsertMultipleRowsAsync_TestRowOrderPreservation()
        {
            if (_skipIntegrationTests)
            {
                Assert.Ignore("サービスアカウントキーが設定されていません");
                return;
            }
            
            // Arrange
            await AuthenticateServiceAccount();
            
            var timestamp = DateTime.Now.Ticks;
            
            // 異なる位置に挿入（行番号ずれのテスト）
            var insertions = new List<(int rowIndex, Dictionary<string, object> rowData)>
            {
                (2, new Dictionary<string, object> 
                { 
                    ["humanId"] = "701",
                    ["name"] = $"First_{timestamp}",
                    ["備考"] = "最初に挿入（位置2）"
                }),
                (2, new Dictionary<string, object> 
                { 
                    ["humanId"] = "702",
                    ["name"] = $"Second_{timestamp}",
                    ["備考"] = "2番目に挿入（同じ位置2）"
                }),
                (4, new Dictionary<string, object> 
                { 
                    ["humanId"] = "703",
                    ["name"] = $"Third_{timestamp}",
                    ["備考"] = "3番目に挿入（位置4）"
                })
            };
            
            // Act
            var result = await _service.InsertMultipleRowsAsync(
                TEST_SPREADSHEET_ID,
                TEST_SHEET_NAME,
                insertions
            );
            
            // Assert
            Assert.IsTrue(result);
            // 降順での挿入により、行番号のずれが防がれることを確認
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex("一括挿入完了: 成功=3, 失敗=0"));
        }
        
        [Test]
        [IntegrationTest]
        public async Task InsertRowAsync_InvalidColumnName_IgnoresInvalidColumns()
        {
            if (_skipIntegrationTests)
            {
                Assert.Ignore("サービスアカウントキーが設定されていません");
                return;
            }
            
            // Arrange
            await AuthenticateServiceAccount();
            
            var rowData = new Dictionary<string, object>
            {
                ["humanId"] = "600",
                ["name"] = "ValidColumn",
                ["invalidColumn"] = "This should be ignored", // 存在しない列
                ["age"] = 25
            };
            
            // Act
            var result = await _service.InsertRowAsync(
                TEST_SPREADSHEET_ID,
                TEST_SHEET_NAME,
                1,
                rowData
            );
            
            // Assert
            Assert.IsTrue(result); // 有効な列があるので成功
            LogAssert.Expect(LogType.Log, "挿入成功: 行を挿入しました。");
        }
        
        #endregion
        
        #region ヘルパーメソッド
        
        /// <summary>
        /// サービスアカウント認証を実行
        /// </summary>
        private async Task AuthenticateServiceAccount()
        {
            if (!GoogleServiceAccountAuth.IsAuthenticated)
            {
                var authResult = await GoogleServiceAccountAuth.AuthorizeAsync(verbose: false);
                if (!authResult)
                {
                    Assert.Fail("サービスアカウント認証に失敗しました");
                }
            }
        }
        
        #endregion
    }
}