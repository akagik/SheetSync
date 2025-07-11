#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SheetSync
{
    /// <summary>
    /// Google API の利用可能性をチェックするユーティリティ
    /// </summary>
    [InitializeOnLoad]
    public static class GoogleApiChecker
    {
        private const string CHECK_KEY = "SheetSync_GoogleApiChecked";
        private static bool isAvailable = false;
        
        static GoogleApiChecker()
        {
            EditorApplication.delayCall += CheckGoogleApiAvailability;
        }
        
        private static void CheckGoogleApiAvailability()
        {
            // セッション中に一度だけチェック
            if (SessionState.GetBool(CHECK_KEY, false))
            {
                return;
            }
            
            SessionState.SetBool(CHECK_KEY, true);
            
            // Google API アセンブリの存在を確認
            var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
            isAvailable = assemblies.Any(a => a.GetName().Name == "Google.Apis.Sheets.v4");
            
            if (!isAvailable)
            {
                Debug.LogWarning(
                    "[SheetSync] Google Sheets API が検出されませんでした。\n" +
                    "Google Sheets API 機能を使用するには:\n" +
                    "1. NuGetForUnity をインストール\n" +
                    "2. Google.Apis.Sheets.v4 パッケージをインストール\n" +
                    "詳細は Documentation/GoogleSheetsAPI_Setup.md を参照してください。"
                );
            }
            else
            {
                Debug.Log("[SheetSync] Google Sheets API が利用可能です。");
            }
        }
        
        /// <summary>
        /// Google API が利用可能かチェック
        /// </summary>
        public static bool IsGoogleApiAvailable()
        {
            if (!isAvailable)
            {
                // 再チェック
                var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
                isAvailable = assemblies.Any(a => a.GetName().Name == "Google.Apis.Sheets.v4");
            }
            
            return isAvailable;
        }
        
        /// <summary>
        /// Google API が利用できない場合に警告を表示
        /// </summary>
        public static bool CheckAndWarn()
        {
            if (!IsGoogleApiAvailable())
            {
                EditorUtility.DisplayDialog(
                    "Google Sheets API が見つかりません",
                    "この機能を使用するには Google Sheets API のインストールが必要です。\n\n" +
                    "1. Window > Package Manager から NuGetForUnity をインストール\n" +
                    "2. NuGet > Manage NuGet Packages から Google.Apis.Sheets.v4 をインストール",
                    "OK"
                );
                
                return false;
            }
            
            return true;
        }
    }
}
#endif
