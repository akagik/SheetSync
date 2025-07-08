using System.IO;
using UnityEditor;
using UnityEngine;

namespace KoheiUtils
{
    public class CsvConverterOldSettingsConverter
    {
        [MenuItem("KoheiUtils/CsvConverter/Convert Old Settings")]
        public static void ConvertOldSettings()
        {
            string[] settingGUIDArray = AssetDatabase.FindAssets("t:CsvConverterSettings");
            
            for (int i = 0; i < settingGUIDArray.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(settingGUIDArray[i]);
                string dirName = System.IO.Path.GetDirectoryName(assetPath);
                
                var settings = AssetDatabase.LoadAssetAtPath<CsvConverterSettings>(assetPath);

                for (int j = 0; j < settings.list.Length; j++)
                {
                    var s = settings.list[j];

                    var newObj = s.ToNewSettings();

                    string newAssetPath = Path.Combine(dirName, $"Convert_{newObj.className}.asset");
                    AssetDatabase.CreateAsset(newObj, newAssetPath);
                    
                    Debug.Log($"Convert into {newAssetPath}");
                }
            }
        }
    }
}