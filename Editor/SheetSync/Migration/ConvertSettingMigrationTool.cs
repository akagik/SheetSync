using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace SheetSync
{
    /// <summary>
    /// KoheiUtils.ConvertSetting から SheetSync.Models.ConvertSetting への移行ツール
    /// 
    /// このツールは、KoheiUtils パッケージで使用されていた ConvertSetting および GlobalCCSettings の
    /// ScriptableObject アセットを、SheetSync パッケージの同等のアセットに変換します。
    /// 
    /// 主な機能:
    /// - Project ウィンドウで選択した KoheiUtils アセットの右クリックメニューから変換を実行
    /// - 元のアセットのすべてのフィールド値を新しいアセットにコピー
    /// - 変換後のファイル名には "_SheetSync" サフィックスを付加
    /// - 元のアセットは削除されず、新しいアセットが作成される
    /// 
    /// 注意事項:
    /// - executeAfterImport フィールドに KoheiUtils.ConvertSetting への参照がある場合、
    ///   手動で SheetSync 版への参照に置き換える必要があります
    /// </summary>
    public static class ConvertSettingMigrationTool
    {
        [MenuItem("Assets/SheetSync/Migrate from KoheiUtils ConvertSetting", true)]
        private static bool ValidateMigrateConvertSetting()
        {
            // KoheiUtils.ConvertSetting 型のアセットが選択されている場合のみ有効
            var selected = Selection.activeObject;
            if (selected == null) return false;
            
            var type = selected.GetType();
            return type.FullName == "KoheiUtils.ConvertSetting";
        }
        
        [MenuItem("Assets/SheetSync/Migrate from KoheiUtils ConvertSetting")]
        private static void MigrateConvertSetting()
        {
            var selected = Selection.activeObject;
            if (selected == null) return;
            
            var originalPath = AssetDatabase.GetAssetPath(selected);
            var directory = Path.GetDirectoryName(originalPath);
            var originalName = Path.GetFileNameWithoutExtension(originalPath);
            
            // 新しいパスを生成（_SheetSync サフィックスを追加）
            var newPath = Path.Combine(directory, $"{originalName}_SheetSync.asset");
            
            // 変換実行
            var success = MigrateConvertSettingAsset(selected, newPath);
            
            if (success)
            {
                Debug.Log($"Successfully migrated: {originalPath} -> {newPath}");
                EditorUtility.DisplayDialog("Migration Success", 
                    $"ConvertSetting を SheetSync 版に変換しました。\n\n元: {originalPath}\n新: {newPath}", 
                    "OK");
                
                // 新しいアセットを選択
                var newAsset = AssetDatabase.LoadAssetAtPath<SheetSync.Models.ConvertSetting>(newPath);
                if (newAsset != null)
                {
                    Selection.activeObject = newAsset;
                    EditorGUIUtility.PingObject(newAsset);
                }
            }
            else
            {
                EditorUtility.DisplayDialog("Migration Failed", 
                    "変換に失敗しました。詳細はコンソールを確認してください。", 
                    "OK");
            }
        }
        
        [MenuItem("Assets/SheetSync/Migrate from KoheiUtils GlobalCCSettings", true)]
        private static bool ValidateMigrateGlobalCCSettings()
        {
            // KoheiUtils.GlobalCCSettings 型のアセットが選択されている場合のみ有効
            var selected = Selection.activeObject;
            if (selected == null) return false;
            
            var type = selected.GetType();
            return type.FullName == "KoheiUtils.GlobalCCSettings";
        }
        
        [MenuItem("Assets/SheetSync/Migrate from KoheiUtils GlobalCCSettings")]
        private static void MigrateGlobalCCSettings()
        {
            var selected = Selection.activeObject;
            if (selected == null) return;
            
            var originalPath = AssetDatabase.GetAssetPath(selected);
            var directory = Path.GetDirectoryName(originalPath);
            var originalName = Path.GetFileNameWithoutExtension(originalPath);
            
            // 新しいパスを生成（_SheetSync サフィックスを追加）
            var newPath = Path.Combine(directory, $"{originalName}_SheetSync.asset");
            
            // 変換実行
            var success = MigrateGlobalCCSettingsAsset(selected, newPath);
            
            if (success)
            {
                Debug.Log($"Successfully migrated: {originalPath} -> {newPath}");
                EditorUtility.DisplayDialog("Migration Success", 
                    $"GlobalCCSettings を SheetSync 版に変換しました。\n\n元: {originalPath}\n新: {newPath}", 
                    "OK");
                
                // 新しいアセットを選択
                var newAsset = AssetDatabase.LoadAssetAtPath<SheetSync.Models.GlobalCCSettings>(newPath);
                if (newAsset != null)
                {
                    Selection.activeObject = newAsset;
                    EditorGUIUtility.PingObject(newAsset);
                }
            }
            else
            {
                EditorUtility.DisplayDialog("Migration Failed", 
                    "変換に失敗しました。詳細はコンソールを確認してください。", 
                    "OK");
            }
        }
        
        internal static bool MigrateConvertSettingAsset(Object koheiUtilsAsset, string newPath)
        {
            try
            {
                // SheetSync版の新しいインスタンスを作成
                var newSetting = ScriptableObject.CreateInstance<SheetSync.Models.ConvertSetting>();
                
                // SerializedObject を使用して値をコピー
                var sourceObj = new SerializedObject(koheiUtilsAsset);
                var targetObj = new SerializedObject(newSetting);
                
                // 各フィールドをコピー
                CopySerializedProperty(sourceObj, targetObj, "csvFilePath");
                CopySerializedProperty(sourceObj, targetObj, "className");
                CopySerializedProperty(sourceObj, targetObj, "checkFullyQualifiedName");
                CopySerializedProperty(sourceObj, targetObj, "destination");
                CopySerializedProperty(sourceObj, targetObj, "codeDestination");
                CopySerializedProperty(sourceObj, targetObj, "isEnum");
                CopySerializedProperty(sourceObj, targetObj, "classGenerate");
                
                // Table 関連
                CopySerializedProperty(sourceObj, targetObj, "tableGenerate");
                CopySerializedProperty(sourceObj, targetObj, "tableClassName");
                CopySerializedProperty(sourceObj, targetObj, "_tableAssetName");
                CopySerializedProperty(sourceObj, targetObj, "tableClassGenerate");
                CopySerializedProperty(sourceObj, targetObj, "isDictionary");
                CopySerializedProperty(sourceObj, targetObj, "onlyTableCreate");
                
                // Join 関連
                CopySerializedProperty(sourceObj, targetObj, "join");
                CopySerializedProperty(sourceObj, targetObj, "targetTable");
                CopySerializedProperty(sourceObj, targetObj, "targetJoinKeyField");
                CopySerializedProperty(sourceObj, targetObj, "selfJoinKeyField");
                CopySerializedProperty(sourceObj, targetObj, "targetJoinListField");
                CopySerializedProperty(sourceObj, targetObj, "targetFindMethodName");
                
                // その他
                CopySerializedProperty(sourceObj, targetObj, "key");
                CopySerializedProperty(sourceObj, targetObj, "executeMethodAfterImport");
                CopySerializedProperty(sourceObj, targetObj, "executeValidationAfterImport");
                CopySerializedProperty(sourceObj, targetObj, "useGSPlugin");
                CopySerializedProperty(sourceObj, targetObj, "sheetID");
                CopySerializedProperty(sourceObj, targetObj, "gid");
                CopySerializedProperty(sourceObj, targetObj, "tempCsvPath");
                CopySerializedProperty(sourceObj, targetObj, "verbose");
                CopySerializedProperty(sourceObj, targetObj, "verboseBtn");
                
                // executeAfterImport は特別処理（ConvertSetting のリストなので変換が必要）
                var executeAfterImportProp = sourceObj.FindProperty("executeAfterImport");
                if (executeAfterImportProp != null && executeAfterImportProp.isArray)
                {
                    var targetList = new List<SheetSync.Models.ConvertSetting>();
                    for (int i = 0; i < executeAfterImportProp.arraySize; i++)
                    {
                        var element = executeAfterImportProp.GetArrayElementAtIndex(i);
                        if (element.objectReferenceValue != null)
                        {
                            // 既存の SheetSync 版を探すか、警告を出す
                            Debug.LogWarning($"executeAfterImport[{i}] の参照は手動で SheetSync 版に置き換える必要があります: {element.objectReferenceValue.name}");
                        }
                    }
                    // 一旦空のリストで初期化
                    targetObj.FindProperty("executeAfterImport").arraySize = 0;
                }
                
                targetObj.ApplyModifiedProperties();
                
                // アセットとして保存
                AssetDatabase.CreateAsset(newSetting, newPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Migration failed: {e.Message}\n{e.StackTrace}");
                return false;
            }
        }
        
        internal static bool MigrateGlobalCCSettingsAsset(Object koheiUtilsAsset, string newPath)
        {
            try
            {
                // SheetSync版の新しいインスタンスを作成
                var newSettings = ScriptableObject.CreateInstance<SheetSync.Models.GlobalCCSettings>();
                
                // SerializedObject を使用して値をコピー
                var sourceObj = new SerializedObject(koheiUtilsAsset);
                var targetObj = new SerializedObject(newSettings);
                
                // 各フィールドをコピー
                CopySerializedProperty(sourceObj, targetObj, "apiKey");
                CopySerializedProperty(sourceObj, targetObj, "useV4");
                CopySerializedProperty(sourceObj, targetObj, "rowIndexOfName");
                CopySerializedProperty(sourceObj, targetObj, "rowIndexOfType");
                CopySerializedProperty(sourceObj, targetObj, "rowIndexOfEnabledColumn");
                CopySerializedProperty(sourceObj, targetObj, "rowIndexOfContentStart");
                CopySerializedProperty(sourceObj, targetObj, "rowIndexOfEnumContentStart");
                CopySerializedProperty(sourceObj, targetObj, "columnIndexOfTableStart");
                CopySerializedProperty(sourceObj, targetObj, "versionFieldName");
                CopySerializedProperty(sourceObj, targetObj, "isEndMarkerEnabled");
                CopySerializedProperty(sourceObj, targetObj, "columnIndexOfEndMarker");
                CopySerializedProperty(sourceObj, targetObj, "endMarker");
                CopySerializedProperty(sourceObj, targetObj, "tempCsvPath");
                CopySerializedProperty(sourceObj, targetObj, "customAssetTypes");
                
                // logType は enum 値をマッピング
                var logTypeProp = sourceObj.FindProperty("logType");
                if (logTypeProp != null)
                {
                    targetObj.FindProperty("logType").enumValueIndex = logTypeProp.enumValueIndex;
                }
                
                targetObj.ApplyModifiedProperties();
                
                // アセットとして保存
                AssetDatabase.CreateAsset(newSettings, newPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Migration failed: {e.Message}\n{e.StackTrace}");
                return false;
            }
        }
        
        private static void CopySerializedProperty(SerializedObject source, SerializedObject target, string propertyName)
        {
            var sourceProp = source.FindProperty(propertyName);
            var targetProp = target.FindProperty(propertyName);
            
            if (sourceProp != null && targetProp != null)
            {
                switch (sourceProp.propertyType)
                {
                    case SerializedPropertyType.String:
                        targetProp.stringValue = sourceProp.stringValue;
                        break;
                    case SerializedPropertyType.Boolean:
                        targetProp.boolValue = sourceProp.boolValue;
                        break;
                    case SerializedPropertyType.Integer:
                        targetProp.intValue = sourceProp.intValue;
                        break;
                    case SerializedPropertyType.Float:
                        targetProp.floatValue = sourceProp.floatValue;
                        break;
                    case SerializedPropertyType.ObjectReference:
                        targetProp.objectReferenceValue = sourceProp.objectReferenceValue;
                        break;
                    case SerializedPropertyType.ArraySize:
                        targetProp.arraySize = sourceProp.arraySize;
                        break;
                    case SerializedPropertyType.Generic:
                        if (sourceProp.isArray)
                        {
                            targetProp.arraySize = sourceProp.arraySize;
                            for (int i = 0; i < sourceProp.arraySize; i++)
                            {
                                var sourceElement = sourceProp.GetArrayElementAtIndex(i);
                                var targetElement = targetProp.GetArrayElementAtIndex(i);
                                
                                // 配列要素の型に応じて処理
                                if (sourceElement.propertyType == SerializedPropertyType.String)
                                {
                                    targetElement.stringValue = sourceElement.stringValue;
                                }
                                // 他の型も必要に応じて追加
                            }
                        }
                        break;
                }
            }
            else if (sourceProp == null)
            {
                Debug.LogWarning($"Source property '{propertyName}' not found");
            }
            else if (targetProp == null)
            {
                Debug.LogWarning($"Target property '{propertyName}' not found");
            }
        }
    }
}
