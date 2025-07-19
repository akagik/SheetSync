using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Util.Store;
using Debug = UnityEngine.Debug;

namespace SheetSync.Services.Auth
{
    /// <summary>
    /// Google OAuth2認証サービス
    /// </summary>
    public class GoogleOAuth2Service
    {
        private const string TOKEN_DIRECTORY = "Library/SheetSync/OAuth2";
        private const string CREDENTIALS_FILE = "credentials.json";
        private static readonly string[] SCOPES = { SheetsService.Scope.Spreadsheets };
        
        private static UserCredential _credential;
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
                    return _credential != null && !IsTokenExpired();
                }
            }
        }
        
        /// <summary>
        /// OAuth2認証を実行
        /// </summary>
        public static async Task<bool> AuthorizeAsync()
        {
            try
            {
                // 認証情報ファイルのパスを取得
                string credPath = GetCredentialsPath();
                if (!File.Exists(credPath))
                {
                    ShowCredentialsSetupDialog();
                    return false;
                }
                
                // 認証フローを開始
                using (var stream = new FileStream(credPath, FileMode.Open, FileAccess.Read))
                {
                    var clientSecrets = GoogleClientSecrets.FromStream(stream);
                    
                    // トークン保存先
                    var dataStore = new FileDataStore(TOKEN_DIRECTORY, true);
                    
                    // 認証フローの設定
                    var initializer = new GoogleAuthorizationCodeFlow.Initializer
                    {
                        ClientSecrets = clientSecrets.Secrets,
                        Scopes = SCOPES,
                        DataStore = dataStore
                    };
                    
                    var flow = new GoogleAuthorizationCodeFlow(initializer);
                    
                    // ローカルサーバーを起動して認証コードを受け取る
                    var authCode = await ReceiveCodeAsync(clientSecrets.Secrets.ClientId);
                    if (string.IsNullOrEmpty(authCode))
                    {
                        Debug.LogError("認証コードの取得に失敗しました。");
                        return false;
                    }
                    
                    // トークンを交換
                    var token = await flow.ExchangeCodeForTokenAsync(
                        "user",
                        authCode,
                        "http://localhost:8080/",
                        CancellationToken.None);
                    
                    _credential = new UserCredential(flow, "user", token);
                    
                    Debug.Log("OAuth2認証に成功しました。");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"OAuth2認証エラー: {ex.Message}");
                Debug.LogException(ex);
                return false;
            }
        }
        
        /// <summary>
        /// 簡易的なOAuth2フロー（デスクトップアプリ用）
        /// </summary>
        public static async Task<bool> AuthorizeSimpleAsync()
        {
            try
            {
                string credPath = GetCredentialsPath();
                if (!File.Exists(credPath))
                {
                    ShowCredentialsSetupDialog();
                    return false;
                }
                
                // credentials.jsonの検証
                try
                {
                    using (var stream = new FileStream(credPath, FileMode.Open, FileAccess.Read))
                    {
                        var clientSecrets = GoogleClientSecrets.FromStream(stream);
                        if (clientSecrets?.Secrets == null)
                        {
                            Debug.LogError("credentials.jsonの形式が正しくありません。");
                            ShowInvalidCredentialsDialog();
                            return false;
                        }
                        
                        // インストール型またはウェブ型のクライアントシークレットをチェック
                        if (string.IsNullOrEmpty(clientSecrets.Secrets.ClientId) || 
                            string.IsNullOrEmpty(clientSecrets.Secrets.ClientSecret))
                        {
                            Debug.LogError("credentials.jsonにclient_idまたはclient_secretが含まれていません。\n" +
                                         "OAuth2クライアントの種類が'デスクトップアプリ'であることを確認してください。");
                            ShowInvalidCredentialsDialog();
                            return false;
                        }
                    }
                }
                catch (Exception validationEx)
                {
                    Debug.LogError($"credentials.jsonの読み込みエラー: {validationEx.Message}\n" +
                                 "ファイルが正しいJSON形式であることを確認してください。");
                    ShowInvalidCredentialsDialog();
                    return false;
                }
                
                // Google認証ライブラリの標準フローを使用
                using (var stream = new FileStream(credPath, FileMode.Open, FileAccess.Read))
                {
                    var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                        GoogleClientSecrets.FromStream(stream).Secrets,
                        SCOPES,
                        "user",
                        CancellationToken.None,
                        new FileDataStore(TOKEN_DIRECTORY, true));
                    
                    _credential = credential;
                    Debug.Log("OAuth2認証に成功しました。");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"OAuth2認証エラー: {ex.Message}");
                if (ex.Message.Contains("At least one client secrets"))
                {
                    Debug.LogError("\n【解決方法】\n" +
                                 "1. Google Cloud Consoleで'デスクトップアプリ'タイプのOAuth2クライアントを作成\n" +
                                 "2. credentials.jsonをダウンロード\n" +
                                 "3. ProjectSettings/SheetSync/credentials.jsonに配置\n" +
                                 "\n詳細は Documentation/OAuth2_Setup_Guide.md を参照してください。");
                    ShowInvalidCredentialsDialog();
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
                throw new InvalidOperationException("OAuth2認証が完了していません。");
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
        [MenuItem("Tools/SheetSync/Clear OAuth2 Token")]
        public static void ClearAuthentication()
        {
            lock (_lock)
            {
                _credential = null;
                
                if (Directory.Exists(TOKEN_DIRECTORY))
                {
                    Directory.Delete(TOKEN_DIRECTORY, true);
                    Debug.Log("OAuth2トークンをクリアしました。");
                }
            }
        }
        
        /// <summary>
        /// 認証状態を確認
        /// </summary>
        [MenuItem("Tools/SheetSync/Check OAuth2 Status")]
        public static void CheckAuthenticationStatus()
        {
            if (IsAuthenticated)
            {
                Debug.Log("OAuth2認証済みです。");
            }
            else
            {
                Debug.Log("OAuth2認証が必要です。");
            }
        }
        
        /// <summary>
        /// credentials.jsonのセットアップダイアログを表示
        /// </summary>
        private static void ShowCredentialsSetupDialog()
        {
            int result = EditorUtility.DisplayDialogComplex(
                "OAuth2認証の設定",
                "Google Cloud Consoleからcredentials.jsonをダウンロードして、\n" +
                $"{GetCredentialsPath()}\nに配置してください。\n\n" +
                "【重要】OAuth2クライアントの種類は'デスクトップアプリ'を選択してください。",
                "設定ガイドを開く",
                "OK",
                "フォルダを開く"
            );
            
            if (result == 0) // 設定方法を開く
            {
                // ローカルのセットアップガイドを開く
                var guidePath = Path.Combine(Application.dataPath.Replace("/Assets", ""), 
                    "Packages/SheetSync/Documentation/OAuth2_Setup_Guide.md");
                if (File.Exists(guidePath))
                {
                    EditorUtility.RevealInFinder(guidePath);
                }
                else
                {
                    Application.OpenURL("https://console.cloud.google.com/apis/credentials");
                }
            }
            else if (result == 2) // フォルダを開く
            {
                var dir = Path.GetDirectoryName(GetCredentialsPath());
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                EditorUtility.RevealInFinder(dir);
            }
        }
        
        /// <summary>
        /// 無効なcredentials.jsonのエラーダイアログを表示
        /// </summary>
        private static void ShowInvalidCredentialsDialog()
        {
            int result = EditorUtility.DisplayDialogComplex(
                "credentials.jsonエラー",
                "credentials.jsonの形式が正しくありません。\n\n" +
                "【確認事項】\n" +
                "1. OAuth2クライアントの種類が'デスクトップアプリ'であること\n" +
                "2. ダウンロードしたJSONファイルを正しく配置していること\n" +
                "3. ファイルが破損していないこと\n\n" +
                "詳細な手順はセットアップガイドを参照してください。",
                "セットアップガイドを開く",
                "Google Cloud Consoleを開く",
                "OK"
            );
            
            if (result == 0) // セットアップガイドを開く
            {
                var guidePath = Path.Combine(Application.dataPath.Replace("/Assets", ""), 
                    "Packages/SheetSync/Documentation/OAuth2_Setup_Guide.md");
                if (File.Exists(guidePath))
                {
                    EditorUtility.RevealInFinder(guidePath);
                }
            }
            else if (result == 1) // Google Cloud Consoleを開く
            {
                Application.OpenURL("https://console.cloud.google.com/apis/credentials");
            }
        }
        
        /// <summary>
        /// credentials.jsonのパスを取得
        /// </summary>
        private static string GetCredentialsPath()
        {
            var dir = Path.Combine(Application.dataPath, "..", "ProjectSettings", "SheetSync");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            return Path.Combine(dir, CREDENTIALS_FILE);
        }
        
        /// <summary>
        /// トークンの有効期限をチェック
        /// </summary>
        private static bool IsTokenExpired()
        {
            if (_credential?.Token?.ExpiresInSeconds == null)
                return true;
                
            // 有効期限の5分前に期限切れとみなす
            var expiryTime = _credential.Token.IssuedUtc.AddSeconds(_credential.Token.ExpiresInSeconds.Value - 300);
            return DateTime.UtcNow >= expiryTime;
        }
        
        /// <summary>
        /// ローカルサーバーで認証コードを受け取る
        /// </summary>
        private static async Task<string> ReceiveCodeAsync(string clientId)
        {
            // ローカルサーバーを起動
            var listener = new TcpListener(IPAddress.Loopback, 8080);
            listener.Start();
            
            try
            {
                // ブラウザで認証ページを開く
                var authUrl = $"https://accounts.google.com/o/oauth2/v2/auth?" +
                    $"client_id={clientId}&" +
                    $"redirect_uri=http://localhost:8080/&" +
                    $"response_type=code&" +
                    $"scope={Uri.EscapeDataString(string.Join(" ", SCOPES))}&" +
                    $"access_type=offline&" +
                    $"prompt=consent";
                    
                Application.OpenURL(authUrl);
                
                // 認証コードを待つ
                using (var client = await listener.AcceptTcpClientAsync())
                using (var stream = client.GetStream())
                {
                    var buffer = new byte[1024];
                    var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    var request = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    
                    // リクエストから認証コードを抽出
                    var codeMatch = System.Text.RegularExpressions.Regex.Match(request, @"code=([^&\s]+)");
                    if (codeMatch.Success)
                    {
                        var code = Uri.UnescapeDataString(codeMatch.Groups[1].Value);
                        
                        // 成功レスポンスを返す
                        var response = "HTTP/1.1 200 OK\r\n" +
                                     "Content-Type: text/html\r\n\r\n" +
                                     "<html><body><h1>認証成功</h1><p>このウィンドウを閉じてUnityに戻ってください。</p></body></html>";
                        await stream.WriteAsync(Encoding.UTF8.GetBytes(response), 0, response.Length);
                        
                        return code;
                    }
                }
            }
            finally
            {
                listener.Stop();
            }
            
            return null;
        }
    }
}