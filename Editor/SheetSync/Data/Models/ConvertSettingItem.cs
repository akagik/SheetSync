using System;
using System.Linq;
using UnityEngine;
using SheetSync;
using KoheiUtils;

namespace SheetSync
{
    /// <summary>
    /// ConvertSetting の表示用モデル
    /// </summary>
    public class ConvertSettingItem
    {
        public ConvertSetting Settings { get; }
        public string AssetPath { get; }
        
        public string DisplayName => GetDisplayName();
        
        public bool CanGenerateCode => Settings.canGenerateCode;
        public bool CanCreateAsset => Settings.canCreateAsset;
        public bool UseGSPlugin => Settings.useGSPlugin;
        public bool IsVerboseMode => Settings.verboseBtn;
        public bool IsJoin => Settings.join;
        
        public UnityEngine.Object OutputReference { get; set; }
        
        public ConvertSettingItem(ConvertSetting settings, string assetPath)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            AssetPath = assetPath;
            UpdateOutputReference();
        }
        
        private string GetDisplayName()
        {
            if (Settings.tableGenerate)
            {
                return Settings.tableAssetName;
            }
            return Settings.className;
        }
        
        public void UpdateOutputReference()
        {
            if (Settings.join)
            {
                OutputReference = Settings.targetTable;
            }
            else
            {
                string mainOutputPath = SheetSync.CCLogic.GetMainOutputPath(Settings);
                if (!string.IsNullOrEmpty(mainOutputPath))
                {
                    OutputReference = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(mainOutputPath);
                }
            }
        }
        
        public bool MatchesSearchText(string searchText)
        {
            if (string.IsNullOrEmpty(searchText))
                return true;

            // 大文字が1文字でも含まれていれば case-sensitive、すべて小文字なら case-insensitive
            bool caseSensitive = searchText.Any(char.IsUpper);

            string search = caseSensitive ? searchText : searchText.ToLowerInvariant();

            string displayName = caseSensitive ? DisplayName : DisplayName.ToLowerInvariant();
            string className = caseSensitive ? Settings.className : Settings.className.ToLowerInvariant();
            string sheetID = caseSensitive ? Settings.sheetID : Settings.sheetID.ToLowerInvariant();

            return search.IsSubsequence(displayName) ||
                   search.IsSubsequence(className) ||
                   search.IsSubsequence(sheetID);
        }
    }
}
