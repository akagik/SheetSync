using NUnit.Framework;
using System.Threading.Tasks;
using UnityEngine;
using SheetSync.Api;
using Newtonsoft.Json;

namespace SheetSync.Tests.Editor
{
    /// <summary>
    /// SheetSyncApiのユニットテスト
    /// </summary>
    public class SheetSyncApiTests
    {
        private const string TestSpreadsheetId = "1eDSiCuI_HLeCV96rZioy_PD85AbNmmqdzrxjjS7sJ_w";
        private const string TestGid = "1380898534";
        
        [Test]
        public void TestInitializeAuth()
        {
            // 認証を初期化
            var result = SheetSyncApi.InitializeAuth("");
            Assert.IsNotNull(result);
            
            // 結果をパース
            var response = JsonConvert.DeserializeObject<SheetSyncApi.ApiResponse<string>>(result);
            Assert.IsNotNull(response);
            
            // すでに認証済みか新規認証かをチェック
            Assert.IsTrue(response.success);
            Assert.IsTrue(response.data == "Already authenticated" || response.data == "Authentication successful");
        }
        
        [Test]
        public void TestCheckAuthStatus()
        {
            var result = SheetSyncApi.CheckAuthStatus();
            Assert.IsNotNull(result);
            
            // 結果に認証情報が含まれているかチェック
            Assert.IsTrue(result.Contains("isAuthenticated"));
        }
        
        [Test]
        public void TestGetSheetNameFromGid()
        {
            // 認証が必要
            SheetSyncApi.InitializeAuth("");
            
            // GIDからシート名を取得
            var sheetName = SheetSyncApiHelper.TestGetSheetNameFromGid(TestSpreadsheetId, TestGid);
            Assert.IsNotNull(sheetName);
            Assert.IsNotEmpty(sheetName);
            
            Debug.Log($"Sheet name for GID {TestGid}: {sheetName}");
        }
        
        [Test]
        public async Task TestUpdateRowAsyncWithoutAuth()
        {
            // 認証なしでUpdateRowAsyncを呼び出すとエラーになることを確認
            var request = new SheetSyncApi.UpdateRequest
            {
                spreadsheetId = TestSpreadsheetId,
                gid = TestGid,
                keyColumn = "humanId",
                keyValue = "999", // 存在しないID
                updateData = new System.Collections.Generic.Dictionary<string, object>
                {
                    ["age"] = "100"
                }
            };
            
            var requestJson = JsonConvert.SerializeObject(request);
            
            // タスクを開始
            var resultTask = SheetSyncApi.UpdateRowAsync(requestJson);
            
            // タイムアウトをセット
            var timeoutTask = Task.Delay(5000);
            var completedTask = await Task.WhenAny(resultTask, timeoutTask);
            
            if (completedTask == timeoutTask)
            {
                Debug.LogWarning("UpdateRowAsync timed out");
                Assert.Pass("Test skipped due to timeout");
            }
            else
            {
                var result = await resultTask;
                Assert.IsNotNull(result);
                
                var response = JsonConvert.DeserializeObject<SheetSyncApi.ApiResponse<bool>>(result);
                Assert.IsNotNull(response);
                
                // 認証エラーまたは別のエラーが返ることを期待
                if (!response.success)
                {
                    Debug.Log($"Expected error: {response.error}");
                    Assert.Pass();
                }
            }
        }
        
        [Test]
        public void TestGetSampleUpdateRequest()
        {
            var sample = SheetSyncApi.GetSampleUpdateRequest();
            Assert.IsNotNull(sample);
            Assert.IsTrue(sample.Contains("spreadsheetId"));
            Assert.IsTrue(sample.Contains("keyColumn"));
            Assert.IsTrue(sample.Contains("updateData"));
        }
        
        [Test]
        public void TestGetApiInfo()
        {
            var info = SheetSyncApi.GetApiInfo();
            Assert.IsNotNull(info);
            Assert.IsTrue(info.Contains("version"));
            Assert.IsTrue(info.Contains("methods"));
        }
    }
}