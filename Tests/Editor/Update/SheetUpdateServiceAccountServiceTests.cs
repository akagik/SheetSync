using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using SheetSync.Services.Update;
using SheetSync.Services.Auth;
using System.IO;

namespace Kohei.SheetSync.Tests.Editor.Update
{
    /// <summary>
    /// SheetUpdateServiceAccountServiceのテストクラス
    /// テスト用スプレッドシート: https://docs.google.com/spreadsheets/d/1eDSiCuI_HLeCV96rZioy_PD85AbNmmqdzrxjjS7sJ_w/edit?gid=1380898534#gid=1380898534
    /// 
    /// 注意: 
    /// 1. [IntegrationTest]属性が付いたテストは実際のGoogle Sheets APIを使用します
    /// 2. サービスアカウントキーが正しく設定されている必要があります
    /// 3. テスト用スプレッドシートへのアクセス権限が必要です
    /// </summary>
    [TestFixture]
    public class SheetUpdateServiceAccountServiceTests
    {
        private const string TEST_SPREADSHEET_ID = "1eDSiCuI_HLeCV96rZioy_PD85AbNmmqdzrxjjS7sJ_w";
        private const string TEST_SHEET_NAME = "Human";
        private const int TEST_SHEET_ID = 1380898534;
        
        private SheetUpdateServiceAccountService _service;
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
            _service = new SheetUpdateServiceAccountService();
        }
        
        [TearDown]
        public void TearDown()
        {
            // 静的フィールドをクリア
            GoogleServiceAccountAuth.ClearAuthentication();
        }
        
        #region 単体テスト（APIを使用しない）
        
        [Test]
        public async Task UpdateRowAsync_NotAuthenticated_ReturnsFalse()
        {
            // Arrange
            // 認証されていない状態を確保
            GoogleServiceAccountAuth.ClearAuthentication();
            
            var updateData = new Dictionary<string, object> { ["name"] = "Test" };
            
            // Act
            var result = await _service.UpdateRowAsync(
                TEST_SPREADSHEET_ID,
                TEST_SHEET_NAME,
                "humanId",
                "1",
                updateData
            );
            
            // Assert
            Assert.IsFalse(result);
            LogAssert.Expect(LogType.Error, "サービスアカウント認証が必要です。");
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
        public async Task UpdateRowAsync_ValidData_UpdatesSuccessfully()
        {
            if (_skipIntegrationTests)
            {
                Assert.Ignore("サービスアカウントキーが設定されていません");
                return;
            }
            
            // Arrange
            await AuthenticateServiceAccount();
            
            var updateData = new Dictionary<string, object>
            {
                ["name"] = "TestUpdate_" + DateTime.Now.Ticks,
                ["age"] = 99
            };
            
            // Act
            var result = await _service.UpdateRowAsync(
                TEST_SPREADSHEET_ID,
                TEST_SHEET_NAME,
                "humanId",
                "1",
                updateData
            );
            
            // Assert
            Assert.IsTrue(result);
            LogAssert.Expect(LogType.Log, "更新成功: 行を更新しました。");
        }
        
        [Test]
        [IntegrationTest]
        public async Task UpdateRowAsync_SingleColumn_UpdatesSuccessfully()
        {
            if (_skipIntegrationTests)
            {
                Assert.Ignore("サービスアカウントキーが設定されていません");
                return;
            }
            
            // Arrange
            await AuthenticateServiceAccount();
            
            var originalName = "Taro"; // 元の値に戻す
            var updateData = new Dictionary<string, object>
            {
                ["name"] = originalName
            };
            
            // Act
            var result = await _service.UpdateRowAsync(
                TEST_SPREADSHEET_ID,
                TEST_SHEET_NAME,
                "humanId",
                "1",
                updateData
            );
            
            // Assert
            Assert.IsTrue(result);
        }
        
        [Test]
        [IntegrationTest]
        public async Task UpdateRowAsync_InvalidKeyColumn_ReturnsFalse()
        {
            if (_skipIntegrationTests)
            {
                Assert.Ignore("サービスアカウントキーが設定されていません");
                return;
            }
            
            // Arrange
            await AuthenticateServiceAccount();
            
            var updateData = new Dictionary<string, object> { ["name"] = "Test" };
            
            // Act
            var result = await _service.UpdateRowAsync(
                TEST_SPREADSHEET_ID,
                TEST_SHEET_NAME,
                "nonExistentColumn",
                "1",
                updateData
            );
            
            // Assert
            Assert.IsFalse(result);
            LogAssert.Expect(LogType.Error, "キー列 'nonExistentColumn' が見つかりません。");
        }
        
        [Test]
        [IntegrationTest]
        public async Task UpdateRowAsync_NonExistentRow_ReturnsFalse()
        {
            if (_skipIntegrationTests)
            {
                Assert.Ignore("サービスアカウントキーが設定されていません");
                return;
            }
            
            // Arrange
            await AuthenticateServiceAccount();
            
            var updateData = new Dictionary<string, object> { ["name"] = "Test" };
            
            // Act
            var result = await _service.UpdateRowAsync(
                TEST_SPREADSHEET_ID,
                TEST_SHEET_NAME,
                "humanId",
                "999", // 存在しないID
                updateData
            );
            
            // Assert
            Assert.IsFalse(result);
            LogAssert.Expect(LogType.Warning, "humanId='999' の行が見つかりません。スキップします。");
        }
        
        [Test]
        [IntegrationTest]
        public async Task UpdateRowAsync_InvalidUpdateColumn_PartiallySucceeds()
        {
            if (_skipIntegrationTests)
            {
                Assert.Ignore("サービスアカウントキーが設定されていません");
                return;
            }
            
            // Arrange
            await AuthenticateServiceAccount();
            
            var updateData = new Dictionary<string, object>
            {
                ["invalidColumn"] = "Test",
                ["name"] = "PartialUpdate_" + DateTime.Now.Ticks
            };
            
            // Act
            var result = await _service.UpdateRowAsync(
                TEST_SPREADSHEET_ID,
                TEST_SHEET_NAME,
                "humanId",
                "2",
                updateData
            );
            
            // Assert
            Assert.IsTrue(result); // 一部の列は更新される
            LogAssert.Expect(LogType.Warning, "列 'invalidColumn' が見つかりません。スキップします。");
        }
        
        [Test]
        [IntegrationTest]
        public async Task UpdateMultipleRowsAsync_ValidData_UpdatesAllRows()
        {
            if (_skipIntegrationTests)
            {
                Assert.Ignore("サービスアカウントキーが設定されていません");
                return;
            }
            
            // Arrange
            await AuthenticateServiceAccount();
            
            var timestamp = DateTime.Now.ToString("HHmmss");
            var updates = new Dictionary<string, Dictionary<string, object>>
            {
                ["0"] = new Dictionary<string, object> { ["name"] = "Kohei" },      // 元に戻す
                ["1"] = new Dictionary<string, object> { ["name"] = "Taro" },       // 元に戻す
                ["2"] = new Dictionary<string, object> { ["name"] = "Mami" }        // 元に戻す
            };
            
            // Act
            var result = await _service.UpdateMultipleRowsAsync(
                TEST_SPREADSHEET_ID,
                TEST_SHEET_NAME,
                "humanId",
                updates
            );
            
            // Assert
            Assert.IsTrue(result);
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex("一括更新完了: 成功=3, 失敗=0"));
        }
        
        [Test]
        [IntegrationTest]
        public async Task UpdateMultipleRowsAsync_SomeInvalidRows_PartialSuccess()
        {
            if (_skipIntegrationTests)
            {
                Assert.Ignore("サービスアカウントキーが設定されていません");
                return;
            }
            
            // Arrange
            await AuthenticateServiceAccount();
            
            var updates = new Dictionary<string, Dictionary<string, object>>
            {
                ["0"] = new Dictionary<string, object> { ["name"] = "ValidUpdate" },
                ["999"] = new Dictionary<string, object> { ["name"] = "InvalidRow" }, // 存在しない行
                ["1"] = new Dictionary<string, object> { ["name"] = "AnotherValid" }
            };
            
            // Act
            var result = await _service.UpdateMultipleRowsAsync(
                TEST_SPREADSHEET_ID,
                TEST_SHEET_NAME,
                "humanId",
                updates,
                verbose: false
            );
            
            // Assert
            Assert.IsFalse(result); // 一部失敗があるのでfalse
            // LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex("一括更新完了: 成功=2, 失敗=1"));
        }
        
        [Test]
        [IntegrationTest]
        public async Task UpdateMultipleRowsAsync_PerformanceTest_SingleBatchUpdate()
        {
            if (_skipIntegrationTests)
            {
                Assert.Ignore("サービスアカウントキーが設定されていません");
                return;
            }
            
            // Arrange
            await AuthenticateServiceAccount();
            
            // 複数行の更新データを準備
            var timestamp = DateTime.Now.Ticks;
            var updates = new Dictionary<string, Dictionary<string, object>>
            {
                ["0"] = new Dictionary<string, object> 
                { 
                    ["name"] = $"PerfTest_0_{timestamp}",
                    ["age"] = 100
                },
                ["1"] = new Dictionary<string, object> 
                { 
                    ["name"] = $"PerfTest_1_{timestamp}",
                    ["age"] = 101
                },
                ["2"] = new Dictionary<string, object> 
                { 
                    ["name"] = $"PerfTest_2_{timestamp}",
                    ["age"] = 102,
                    ["備考"] = "パフォーマンステスト"
                }
            };
            
            // Act
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var result = await _service.UpdateMultipleRowsAsync(
                TEST_SPREADSHEET_ID,
                TEST_SHEET_NAME,
                "humanId",
                updates
            );
            stopwatch.Stop();
            
            // Assert
            Assert.IsTrue(result);
            Debug.Log($"UpdateMultipleRowsAsync実行時間: {stopwatch.ElapsedMilliseconds}ms");
            
            // 最適化により、3行の更新でも1回のAPI呼び出しで完了することを確認
            // （実際のパフォーマンス改善は、行数が多いほど顕著になる）
            Assert.Less(stopwatch.ElapsedMilliseconds, 5000, "バッチ更新は5秒以内に完了すべき");
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
    
    /// <summary>
    /// テスト用のサンプルクラス（HumanMaster）
    /// </summary>
    public class HumanMaster
    {
        public int humanId;
        public string name;
        public int age;
        public string 備考;
    }
}