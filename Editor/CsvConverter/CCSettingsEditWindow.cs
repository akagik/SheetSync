using System.IO;
using UnityEngine;

namespace KoheiUtils
{
    using UnityEditor;
#if ODIN_INSPECTOR
    using Sirenix.OdinInspector;
    using Sirenix.OdinInspector.Editor;

    public class CCSettingsEditWindow : OdinEditorWindow
    {
        public string className;
        public string sheetId;
        public string gid;
        public string key;
        public bool   isDictionary;

//        string                       settingPath;
        ConvertSetting settings;

        private bool createNew;
        private string newAssetDir;

        public static CCSettingsEditWindow OpenWindow()
        {
            var window = GetWindow<CCSettingsEditWindow>();
            window.Show();
            return window;
        }

        public void SetSettings(ConvertSetting setting)
        {
            this.settings = setting;
            this.createNew = false;

            className    = setting.className;
            sheetId      = setting.sheetID;
            gid          = setting.gid;
            key          = setting.key;
            isDictionary = setting.isDictionary;
        }
        
        public void SetNewSettings(ConvertSetting setting, string newAssetDir)
        {
            this.settings = setting;
            this.createNew = true;
            this.newAssetDir = newAssetDir;

            className    = setting.className;
            sheetId      = setting.sheetID;
            gid          = setting.gid;
            key          = setting.key;
            isDictionary = setting.isDictionary;
        }

        [Button(ButtonSizes.Large), GUIColor(0, 1, 0)]
        public void SaveSettings()
        {
            settings.className    = className;
            settings.sheetID      = sheetId;
            settings.gid          = gid;
            settings.key          = key;
            settings.isDictionary = isDictionary;

            if (createNew)
            {
                string newPath = Path.Combine(newAssetDir, $"Convert_{className}.asset");
                AssetDatabase.CreateAsset(settings, newPath);
            }

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Close();
        }
    }

#else
    public class CCSettingsEditWindow : EditorWindow
    {
    }
#endif
}