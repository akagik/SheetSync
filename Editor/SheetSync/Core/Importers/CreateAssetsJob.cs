using System;
using SheetSync;
using UnityEditor;
using UnityEngine;

namespace SheetSync
{
    public class CreateAssetsJob
    {
        public SheetSync.Models.ConvertSetting settings;
        private ICsvDataProvider dataProvider;
        // public string settingPath => settings.GetDirectoryPath();

        public CreateAssetsJob(SheetSync.Models.ConvertSetting settings)
        {
            this.settings    = settings;
        }
        
        public CreateAssetsJob(SheetSync.Models.ConvertSetting settings, ICsvDataProvider dataProvider)
        {
            this.settings = settings;
            this.dataProvider = dataProvider;
        }

        public object Execute()
        {
            SheetSync.Models.GlobalCCSettings gSettings = CCLogic.GetGlobalSettings();

            object generated = null;
            
            try
            {
                // データプロバイダーが設定されていない場合は、SheetSyncService から取得
                if (dataProvider == null)
                {
                    dataProvider = SheetSyncService.GetCsvDataProvider(settings, gSettings);
                }
                
                generated = CsvConvert.CreateAssets(settings, gSettings, dataProvider);
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
