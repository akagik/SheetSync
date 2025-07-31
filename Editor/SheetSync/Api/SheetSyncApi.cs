using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using SheetSync.Services.Update;
using SheetSync.Services.Insert;
using SheetSync.Services.Auth;
using SheetSync.Services;
using Newtonsoft.Json;
using System.IO;
using KoheiUtils;

namespace SheetSync.Api
{
    /// <summary>
    /// SheetSync の外部API用静的ラッパークラス
    /// MCP経由でUnity外部から呼び出すためのエントリーポイントを提供します。
    /// 
    /// 設計原則：
    /// 1. すべてのメソッドは静的メソッドとして実装
    /// 2. 非同期処理は同期的に実行して結果を返す
    /// 3. エラーはApiResponseに含めて返す（例外を投げない）
    /// 4. 複雑な型はJSON文字列として受け渡し
    /// </summary>
    public static class SheetSyncApi
    {
        #region 共通データ構造

        /// <summary>
        /// API呼び出しの統一レスポンス
        /// </summary>
        [Serializable]
        public class ApiResponse<T>
        {
            public bool success;
            public T data;
            public string error;
            public string details;

            public static ApiResponse<T> Success(T data)
            {
                return new ApiResponse<T>
                {
                    success = true,
                    data = data
                };
            }

            public static ApiResponse<T> Error(string error, string details = null)
            {
                return new ApiResponse<T>
                {
                    success = false,
                    error = error,
                    details = details
                };
            }
        }

        /// <summary>
        /// 更新データをJSON形式で受け取るための構造
        /// </summary>
        [Serializable]
        public class UpdateRequest
        {
            public string spreadsheetId;
            public string sheetName;  // オプション：gidが指定された場合は無視される
            public string gid;        // オプション：sheetNameより優先される
            public string keyColumn;
            public string keyValue;
            public Dictionary<string, object> updateData;
        }

        /// <summary>
        /// 複数行更新データをJSON形式で受け取るための構造
        /// </summary>
        [Serializable]
        public class BatchUpdateRequest
        {
            public string spreadsheetId;
            public string sheetName;  // オプション：gidが指定された場合は無視される
            public string gid;        // オプション：sheetNameより優先される
            public string keyColumn;
            public Dictionary<string, Dictionary<string, object>> updates;
        }

        #endregion

        #region 認証関連

        /// <summary>
        /// サービスアカウント認証の初期化
        /// </summary>
        /// <param name="credentialsPath">認証情報ファイルのパス（nullの場合はデフォルトパスを使用）</param>
        /// <returns>JSON形式のApiResponse</returns>
        public static string InitializeAuth(string credentialsPath = null)
        {
            try
            {
                // 既に認証済みの場合
                if (GoogleServiceAccountAuth.IsAuthenticated)
                {
                    return JsonConvert.SerializeObject(ApiResponse<string>.Success("Already authenticated"));
                }

                // 認証実行（非同期メソッドを同期的に実行）
                var task = Task.Run(async () => await GoogleServiceAccountAuth.AuthorizeAsync(verbose: true));
                
                if (task.Wait(TimeSpan.FromSeconds(10)))
                {
                    bool result = task.Result;
                    if (result)
                    {
                        return JsonConvert.SerializeObject(ApiResponse<string>.Success("Authentication successful"));
                    }
                    else
                    {
                        return JsonConvert.SerializeObject(ApiResponse<string>.Error("Authentication failed. Check service account key file."));
                    }
                }
                else
                {
                    return JsonConvert.SerializeObject(ApiResponse<string>.Error("Authentication timeout"));
                }
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(ApiResponse<string>.Error("Authentication error", ex.Message));
            }
        }

        /// <summary>
        /// 認証状態の確認
        /// </summary>
        /// <returns>JSON形式のApiResponse</returns>
        public static string CheckAuthStatus()
        {
            var status = new
            {
                isAuthenticated = GoogleServiceAccountAuth.IsAuthenticated,
                keyFilePath = GetServiceAccountKeyPath(),
                keyFileExists = File.Exists(GetServiceAccountKeyPath())
            };
            
            return JsonConvert.SerializeObject(ApiResponse<object>.Success(status));
        }
        
        /// <summary>
        /// サービスアカウントキーのデフォルトパスを取得
        /// </summary>
        private static string GetServiceAccountKeyPath()
        {
            var dir = Path.Combine(UnityEngine.Application.dataPath, "..", "ProjectSettings", "SheetSync");
            return Path.Combine(dir, "service-account-key.json");
        }

        #endregion

        #region 更新系API

        /// <summary>
        /// 単一行の更新
        /// </summary>
        /// <param name="requestJson">UpdateRequest構造のJSON文字列</param>
        /// <returns>JSON形式のApiResponse</returns>
        public static string UpdateRow(string requestJson)
        {
            try
            {
                // リクエストのパース
                var request = JsonConvert.DeserializeObject<UpdateRequest>(requestJson);
                
                // 入力検証
                if (request == null)
                {
                    return JsonConvert.SerializeObject(ApiResponse<bool>.Error("Invalid request format"));
                }

                if (string.IsNullOrEmpty(request.spreadsheetId) || 
                    string.IsNullOrEmpty(request.keyColumn) ||
                    string.IsNullOrEmpty(request.keyValue))
                {
                    return JsonConvert.SerializeObject(ApiResponse<bool>.Error("Missing required parameters"));
                }
                
                // sheetNameとgidの両方が空の場合はエラー
                if (string.IsNullOrEmpty(request.sheetName) && string.IsNullOrEmpty(request.gid))
                {
                    return JsonConvert.SerializeObject(ApiResponse<bool>.Error("Either sheetName or gid must be provided"));
                }

                // GIDが指定されている場合はシート名を取得
                string actualSheetName = request.sheetName;
                if (!string.IsNullOrEmpty(request.gid))
                {
                    try
                    {
                        // 認証チェック
                        if (!GoogleServiceAccountAuth.IsAuthenticated)
                        {
                            return JsonConvert.SerializeObject(ApiResponse<bool>.Error("Service account authentication required"));
                        }
                        
                        var authService = GoogleServiceAccountAuth.GetAuthenticatedService();
                        var sheetNameTask = Task.Run(async () => 
                            await GoogleSheetsUtility.GetSheetNameFromGidAsync(authService, request.spreadsheetId, request.gid)
                        );
                        
                        if (sheetNameTask.Wait(TimeSpan.FromSeconds(10)))
                        {
                            actualSheetName = sheetNameTask.Result;
                            if (string.IsNullOrEmpty(actualSheetName))
                            {
                                return JsonConvert.SerializeObject(ApiResponse<bool>.Error($"Sheet with GID '{request.gid}' not found"));
                            }
                        }
                        else
                        {
                            return JsonConvert.SerializeObject(ApiResponse<bool>.Error("Timeout while getting sheet name from GID"));
                        }
                    }
                    catch (Exception ex)
                    {
                        return JsonConvert.SerializeObject(ApiResponse<bool>.Error("Failed to get sheet name from GID", ex.Message));
                    }
                }
                
                // サービスインスタンスを作成
                var service = new SheetUpdateServiceAccountService();
                
                // 非同期メソッドを同期的に実行
                var task = Task.Run(async () => await service.UpdateRowAsync(
                    request.spreadsheetId,
                    actualSheetName,
                    request.keyColumn,
                    request.keyValue,
                    request.updateData,
                    verbose: true
                ));
                
                // タイムアウト付きで待機（30秒）
                if (task.Wait(TimeSpan.FromSeconds(30)))
                {
                    bool result = task.Result;
                    return JsonConvert.SerializeObject(ApiResponse<bool>.Success(result));
                }
                else
                {
                    return JsonConvert.SerializeObject(ApiResponse<bool>.Error("Operation timeout"));
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return JsonConvert.SerializeObject(ApiResponse<bool>.Error("Update failed", ex.Message));
            }
        }

        /// <summary>
        /// 複数行の一括更新
        /// </summary>
        /// <param name="requestJson">BatchUpdateRequest構造のJSON文字列</param>
        /// <returns>JSON形式のApiResponse</returns>
        public static string UpdateMultipleRows(string requestJson)
        {
            try
            {
                // リクエストのパース
                var request = JsonConvert.DeserializeObject<BatchUpdateRequest>(requestJson);
                
                // 入力検証
                if (request == null)
                {
                    return JsonConvert.SerializeObject(ApiResponse<bool>.Error("Invalid request format"));
                }

                if (string.IsNullOrEmpty(request.spreadsheetId) || 
                    string.IsNullOrEmpty(request.keyColumn))
                {
                    return JsonConvert.SerializeObject(ApiResponse<bool>.Error("Missing required parameters"));
                }
                
                // sheetNameとgidの両方が空の場合はエラー
                if (string.IsNullOrEmpty(request.sheetName) && string.IsNullOrEmpty(request.gid))
                {
                    return JsonConvert.SerializeObject(ApiResponse<bool>.Error("Either sheetName or gid must be provided"));
                }

                if (request.updates == null || request.updates.Count == 0)
                {
                    return JsonConvert.SerializeObject(ApiResponse<bool>.Error("No update data provided"));
                }

                // GIDが指定されている場合はシート名を取得
                string actualSheetName = request.sheetName;
                if (!string.IsNullOrEmpty(request.gid))
                {
                    try
                    {
                        // 認証チェック
                        if (!GoogleServiceAccountAuth.IsAuthenticated)
                        {
                            return JsonConvert.SerializeObject(ApiResponse<bool>.Error("Service account authentication required"));
                        }
                        
                        var authService = GoogleServiceAccountAuth.GetAuthenticatedService();
                        var sheetNameTask = Task.Run(async () => 
                            await GoogleSheetsUtility.GetSheetNameFromGidAsync(authService, request.spreadsheetId, request.gid)
                        );
                        
                        if (sheetNameTask.Wait(TimeSpan.FromSeconds(10)))
                        {
                            actualSheetName = sheetNameTask.Result;
                            if (string.IsNullOrEmpty(actualSheetName))
                            {
                                return JsonConvert.SerializeObject(ApiResponse<bool>.Error($"Sheet with GID '{request.gid}' not found"));
                            }
                        }
                        else
                        {
                            return JsonConvert.SerializeObject(ApiResponse<bool>.Error("Timeout while getting sheet name from GID"));
                        }
                    }
                    catch (Exception ex)
                    {
                        return JsonConvert.SerializeObject(ApiResponse<bool>.Error("Failed to get sheet name from GID", ex.Message));
                    }
                }
                
                // サービスインスタンスを作成
                var service = new SheetUpdateServiceAccountService();
                
                // 非同期メソッドを同期的に実行
                var task = Task.Run(async () => await service.UpdateMultipleRowsAsync(
                    request.spreadsheetId,
                    actualSheetName,
                    request.keyColumn,
                    request.updates,
                    verbose: true
                ));
                
                // タイムアウト付きで待機（60秒）
                if (task.Wait(TimeSpan.FromSeconds(60)))
                {
                    bool result = task.Result;
                    var response = new
                    {
                        success = result,
                        rowCount = request.updates.Count
                    };
                    return JsonConvert.SerializeObject(ApiResponse<object>.Success(response));
                }
                else
                {
                    return JsonConvert.SerializeObject(ApiResponse<bool>.Error("Operation timeout"));
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return JsonConvert.SerializeObject(ApiResponse<bool>.Error("Batch update failed", ex.Message));
            }
        }

        #endregion

        #region ユーティリティメソッド

        /// <summary>
        /// サンプルリクエストの生成（開発/テスト用）
        /// </summary>
        /// <returns>サンプルUpdateRequestのJSON文字列</returns>
        public static string GetSampleUpdateRequest()
        {
            var sample = new UpdateRequest
            {
                spreadsheetId = "your-spreadsheet-id",
                sheetName = "Sheet1",  // または gid を使用
                gid = "1380898534",     // sheetName の代わりに使用可能
                keyColumn = "ID",
                keyValue = "123",
                updateData = new Dictionary<string, object>
                {
                    ["Name"] = "Updated Name",
                    ["Age"] = "30",
                    ["Status"] = "Active"
                }
            };
            
            return JsonConvert.SerializeObject(sample, Formatting.Indented);
        }

        /// <summary>
        /// サンプルバッチリクエストの生成（開発/テスト用）
        /// </summary>
        /// <returns>サンプルBatchUpdateRequestのJSON文字列</returns>
        public static string GetSampleBatchUpdateRequest()
        {
            var sample = new BatchUpdateRequest
            {
                spreadsheetId = "your-spreadsheet-id",
                sheetName = "Sheet1",  // または gid を使用
                gid = "1380898534",     // sheetName の代わりに使用可能
                keyColumn = "ID",
                updates = new Dictionary<string, Dictionary<string, object>>
                {
                    ["123"] = new Dictionary<string, object>
                    {
                        ["Name"] = "User 123",
                        ["Status"] = "Active"
                    },
                    ["456"] = new Dictionary<string, object>
                    {
                        ["Name"] = "User 456",
                        ["Status"] = "Inactive"
                    }
                }
            };
            
            return JsonConvert.SerializeObject(sample, Formatting.Indented);
        }

        /// <summary>
        /// API情報の取得
        /// </summary>
        /// <returns>利用可能なAPIメソッドの情報</returns>
        public static string GetApiInfo()
        {
            var info = new
            {
                version = "1.0.0",
                methods = new[]
                {
                    new { name = "InitializeAuth", description = "サービスアカウント認証の初期化" },
                    new { name = "CheckAuthStatus", description = "認証状態の確認" },
                    new { name = "UpdateRow", description = "単一行の更新" },
                    new { name = "UpdateMultipleRows", description = "複数行の一括更新" },
                    new { name = "GetSampleUpdateRequest", description = "サンプルリクエストの取得" },
                    new { name = "GetSampleBatchUpdateRequest", description = "サンプルバッチリクエストの取得" }
                },
                authentication = "Google Service Account required"
            };
            
            return JsonConvert.SerializeObject(info, Formatting.Indented);
        }

        #endregion

        #region 拡張用プレースホルダー

        // TODO: 今後実装予定のサービス用メソッド
        // - InsertRow / InsertMultipleRows (SheetInsertServiceAccountService)
        // - DeleteRow / DeleteMultipleRows (削除サービス実装後)
        // - GetSheetData (読み込みサービス)
        // - CreateSheet / DeleteSheet (シート管理)

        #endregion
    }
}