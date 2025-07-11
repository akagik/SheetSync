using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using SheetSync;

namespace SheetSync
{
    /// <summary>
    /// ConvertSetting の取得・管理を行うリポジトリ
    /// 
    /// Repository パターンを使用して、ConvertSetting データへのアクセスを抽象化します。
    /// Unity の AssetDatabase を使用してプロジェクト内の ConvertSetting アセットを
    /// 検索、読み込み、キャッシュ管理を行います。
    /// 
    /// 主な責任:
    /// - ConvertSetting アセットの検索と読み込み
    /// - メモリキャッシュによるパフォーマンス最適化
    /// - 検索条件によるフィルタリング
    /// - グローバル設定へのアクセス
    /// 
    /// 設計上の特徴:
    /// - データアクセスロジックをViewModelから分離
    /// - キャッシュ機能によるパフォーマンス向上
    /// - Unity エディタのライフサイクルに対応した実装
    /// </summary>
    public class SheetSyncRepository
    {
        private List<ConvertSettingItem> _cachedItems;
        
        /// <summary>
        /// すべての ConvertSetting を取得します
        /// </summary>
        /// <returns>キャッシュされた ConvertSettingItem のリスト（名前順でソート済み）</returns>
        /// <remarks>
        /// 初回呼び出し時は AssetDatabase から読み込み、以降はキャッシュから返します。
        /// </remarks>
        public List<ConvertSettingItem> GetAllSettings()
        {
            if (_cachedItems != null)
                return _cachedItems;
                
            RefreshCache();
            return _cachedItems;
        }
        
        /// <summary>
        /// キャッシュを更新します
        /// </summary>
        /// <remarks>
        /// プロジェクト全体を再スキャンし、ConvertSetting アセットを再読み込みします。
        /// アセットの追加、削除、移動などがあった場合に呼び出す必要があります。
        /// </remarks>
        public void RefreshCache()
        {
            _cachedItems = new List<ConvertSettingItem>();
            
            string[] settingGUIDs = AssetDatabase.FindAssets("t:ConvertSetting");
            foreach (var guid in settingGUIDs)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var settings = AssetDatabase.LoadAssetAtPath<ConvertSetting>(assetPath);
                
                if (settings != null)
                {
                    var item = new ConvertSettingItem(settings, assetPath);
                    _cachedItems.Add(item);
                }
            }
            
            // 名前順でソート
            _cachedItems.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.Ordinal));
        }
        
        /// <summary>
        /// 検索条件に一致する設定を取得します
        /// </summary>
        /// <param name="searchText">検索テキスト（null または空の場合はすべてを返す）</param>
        /// <returns>検索条件に一致する ConvertSettingItem のコレクション</returns>
        /// <remarks>
        /// ConvertSettingItem.MatchesSearchText メソッドを使用してフィルタリングを行います。
        /// </remarks>
        public IEnumerable<ConvertSettingItem> GetFilteredSettings(string searchText)
        {
            var allSettings = GetAllSettings();
            
            if (string.IsNullOrEmpty(searchText))
                return allSettings;
                
            return allSettings.Where(item => item.MatchesSearchText(searchText));
        }
        
        /// <summary>
        /// グローバル設定を取得します
        /// </summary>
        /// <returns>SheetSync のグローバル設定オブジェクト</returns>
        /// <remarks>
        /// CCLogic を通じてグローバル設定にアクセスします。
        /// これらの設定はコード生成やアセット作成時に使用されます。
        /// </remarks>
        public GlobalCCSettings GetGlobalSettings()
        {
            return SheetSync.CCLogic.GetGlobalSettings();
        }
    }
}
