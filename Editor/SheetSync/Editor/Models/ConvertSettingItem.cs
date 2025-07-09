using System;
using UnityEngine;
using SheetSync.Models;

namespace SheetSync.Editor.Models
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
                
            var searchLower = searchText.ToLowerInvariant();
            var displayNameLower = DisplayName.ToLowerInvariant();
            
            // IsSubsequence拡張メソッドがKoheiUtilsにあるため、contains で代替
            return displayNameLower.Contains(searchLower) ||
                   Settings.className.ToLowerInvariant().Contains(searchLower) ||
                   Settings.sheetID.ToLowerInvariant().Contains(searchLower);
        }
    }
}