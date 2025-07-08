using System;
using UnityEditor;
using UnityEngine;

namespace KoheiUtils
{
    public class CreateAssetsJob
    {
        public ConvertSetting settings;
        public string settingPath => settings.GetDirectoryPath();

        public CreateAssetsJob(ConvertSetting settings)
        {
            this.settings    = settings;
        }

        public object Execute()
        {
            GlobalCCSettings gSettings = CCLogic.GetGlobalSettings();

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