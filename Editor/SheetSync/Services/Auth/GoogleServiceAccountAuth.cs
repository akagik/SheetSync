using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Util.Store;

namespace SheetSync.Services.Auth
{
    /// <summary>
    /// Google サービスアカウント認証サービス
    /// </summary>
    public class GoogleServiceAccountAuth
    {
        private const string SERVICE_ACCOUNT_KEY_FILE = "service-account-key.json";
        private static readonly string[] SCOPES = { SheetsService.Scope.Spreadsheets };
        
        private static ServiceAccountCredential _credential;
        private static readonly object _lock = new object();
        
        /// <summary>
        /// 認証済みかどうか
        /// </summary>
        public static bool IsAuthenticated
        {
            get
            {
                lock (_lock)
                {
                    return _credential != null;
                }
            }
        }
        
        /// <summary>
        /// サービスアカウント認証を実行
        /// </summary>
        public static async Task<bool> AuthorizeAsync(bool verbose = true)
        {
            try
            {
                string keyPath = GetServiceAccountKeyPath();
                if (!File.Exists(keyPath))
                {
                    ShowServiceAccountSetupDialog();
                    return false;
                }
                
                // サービスアカウントキーを読み込み
                string jsonKey = File.ReadAllText(keyPath);
                
                // サービスアカウント認証情報を作成
                var credential = GoogleCredential.FromJson(jsonKey)
                    .CreateScoped(SCOPES)
                    .UnderlyingCredential as ServiceAccountCredential;
                
                if (credential == null)
                {
                    throw new InvalidOperationException("サービスアカウント認証情報の作成に失敗しました。");
                }
                
                _credential = credential;
                
                // 認証をテスト
                var service = GetAuthenticatedService();
                if (service != null)
                {
                    if (verbose) Debug.Log("サービスアカウント認証に成功しました。");
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"サービスアカウント認証エラー: {ex.Message}");
                if (ex.Message.Contains("private_key"))
                {
                    Debug.LogError("\n【解決方法】\n" +
                                 "1. Google Cloud Consoleでサービスアカウントを作成\n" +
                                 "2. キーを作成（JSON形式）\n" +
                                 "3. ダウンロードしたファイルをservice-account-key.jsonにリネーム\n" +
                                 "4. ProjectSettings/SheetSync/に配置");
                }
                Debug.LogException(ex);
                return false;
            }
        }
        
        /// <summary>
        /// 認証済みのSheetsServiceを取得
        /// </summary>
        public static SheetsService GetAuthenticatedService()
        {
            if (!IsAuthenticated)
            {
                throw new InvalidOperationException("サービスアカウント認証が完了していません。");
            }
            
            return new SheetsService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = _credential,
                ApplicationName = "SheetSync",
            });
        }
        
        /// <summary>
        /// 認証をクリア
        /// </summary>
        [MenuItem("Tools/SheetSync/Clear Service Account Auth")]
        public static void ClearAuthentication()
        {
            lock (_lock)
            {
                _credential = null;
                // Debug.Log("サービスアカウント認証をクリアしました。");
            }
        }
        
        /// <summary>
        /// 認証状態を確認
        /// </summary>
        [MenuItem("Tools/SheetSync/Check Service Account Status")]
        public static void CheckAuthenticationStatus()
        {
            if (IsAuthenticated)
            {
                Debug.Log("サービスアカウント認証済みです。");
                var keyPath = GetServiceAccountKeyPath();
                if (File.Exists(keyPath))
                {
                    try
                    {
                        var json = File.ReadAllText(keyPath);
                        if (json.Contains("\"client_email\":"))
                        {
                            var emailStart = json.IndexOf("\"client_email\":") + 16;
                            var emailEnd = json.IndexOf("\"", emailStart);
                            var email = json.Substring(emailStart, emailEnd - emailStart);
                            Debug.Log($"サービスアカウント: {email}");
                        }
                    }
                    catch { }
                }
            }
            else
            {
                Debug.Log("サービスアカウント認証が必要です。");
            }
        }
        
        /// <summary>
        /// サービスアカウントキーのセットアップダイアログを表示
        /// </summary>
        private static void ShowServiceAccountSetupDialog()
        {
            int result = EditorUtility.DisplayDialogComplex(
                "サービスアカウント認証の設定",
                "Google Cloud Consoleからサービスアカウントキー（JSON）をダウンロードして、\n" +
                $"{GetServiceAccountKeyPath()}\nに配置してください。\n\n" +
                "【重要】スプレッドシートをサービスアカウントのメールアドレスと共有する必要があります。",
                "設定ガイドを開く",
                "OK",
                "フォルダを開く"
            );
            
            if (result == 0) // 設定方法を開く
            {
                var guidePath = Path.Combine(Application.dataPath.Replace("/Assets", ""), 
                    "Packages/SheetSync/Documentation/ServiceAccount_Setup_Guide.md");
                if (File.Exists(guidePath))
                {
                    EditorUtility.RevealInFinder(guidePath);
                }
                else
                {
                    Application.OpenURL("https://console.cloud.google.com/iam-admin/serviceaccounts");
                }
            }
            else if (result == 2) // フォルダを開く
            {
                var dir = Path.GetDirectoryName(GetServiceAccountKeyPath());
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                EditorUtility.RevealInFinder(dir);
            }
        }
        
        /// <summary>
        /// サービスアカウントキーのパスを取得
        /// </summary>
        private static string GetServiceAccountKeyPath()
        {
            var dir = Path.Combine(Application.dataPath, "..", "ProjectSettings", "SheetSync");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            return Path.Combine(dir, SERVICE_ACCOUNT_KEY_FILE);
        }
    }
}