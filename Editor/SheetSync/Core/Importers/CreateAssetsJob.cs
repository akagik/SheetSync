using System;
using SheetSync.Models;
using UnityEditor;
using UnityEngine;

namespace SheetSync
{
    public class CreateAssetsJob
    {
        public SheetSync.Models.ConvertSetting settings;
        // public string settingPath => settings.GetDirectoryPath();

        public CreateAssetsJob(SheetSync.Models.ConvertSetting settings)
        {
            this.settings    = settings;
        }

        public object Execute()
        {
            SheetSync.Models.GlobalCCSettings gSettings = CCLogic.GetGlobalSettings();

            object generated = null;
            
            try
            {
                generated = CsvConvert.CreateAssets(settings, gSettings);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.ClearProgressBar();

            return generated;
        }
    }
}