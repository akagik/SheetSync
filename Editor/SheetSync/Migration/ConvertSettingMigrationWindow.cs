using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace SheetSync.Migration
{
    /// <summary>
    /// KoheiUtils から SheetSync への一括移行ウィンドウ
    /// 
    /// この EditorWindow は、プロジェクト内のすべての KoheiUtils.ConvertSetting および
    /// KoheiUtils.GlobalCCSettings アセットを一覧表示し、選択したアセットを
    /// 一括で SheetSync 版に変換する機能を提供します。
    /// 
    /// 主な機能:
    /// - プロジェクト内の KoheiUtils アセットを自動検出
    /// - チェックボックスで変換対象を選択
    /// - すでに移行済みのアセットを自動識別
    /// - 進捗バー付きの一括変換処理
    /// - 変換結果のレポート表示
    /// 
    /// 使用方法:
    /// 1. メニューから "SheetSync/Migration/Batch Migration Tool" を選択
    /// 2. 変換したいアセットにチェックを入れる
    /// 3. "選択したアセットを移行" ボタンをクリック
    /// 
    /// 注意事項:
    /// - 元のアセットは削除されず、新しいアセットが作成されます
    /// - executeAfterImport の参照は手動での更新が必要です
    /// </summary>
    public class ConvertSettingMigrationWindow : EditorWindow
    {
        private List<MigrationItem> convertSettingItems = new List<MigrationItem>();
        private List<MigrationItem> globalSettingsItems = new List<MigrationItem>();
        private Vector2 scrollPosition;
        private bool showConvertSettings = true;
        private bool showGlobalSettings = true;
        
        private class MigrationItem
        {
            public Object originalAsset;
            public string originalPath;
            public string newPath;
            public bool selected = true;
            public bool alreadyMigrated;
        }
        
        [MenuItem("SheetSync/Migration/Batch Migration Tool")]
        public static void OpenWindow()
        {
            var window = GetWindow<ConvertSettingMigrationWindow>("SheetSync Migration Tool");
            window.minSize = new Vector2(600, 400);
            window.Show();
        }
        
        private void OnEnable()
        {
            RefreshAssetList();
        }
        
        private void RefreshAssetList()
        {
            convertSettingItems.Clear();
            globalSettingsItems.Clear();
            
            // すべての KoheiUtils.ConvertSetting を検索
            var convertSettingGuids = AssetDatabase.FindAssets("t:ScriptableObject");
            foreach (var guid in convertSettingGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                
                if (asset != null && asset.GetType().FullName == "KoheiUtils.ConvertSetting")
                {
                    var item = new MigrationItem
                    {
                        originalAsset = asset,
                        originalPath = path,
                        newPath = GenerateNewPath(path)
                    };
                    item.alreadyMigrated = AssetDatabase.LoadAssetAtPath<Object>(item.newPath) != null;
                    convertSettingItems.Add(item);
                }
                else if (asset != null && asset.GetType().FullName == "KoheiUtils.GlobalCCSettings")
                {
                    var item = new MigrationItem
                    {
                        originalAsset = asset,
                        originalPath = path,
                        newPath = GenerateNewPath(path)
                    };
                    item.alreadyMigrated = AssetDatabase.LoadAssetAtPath<Object>(item.newPath) != null;
                    globalSettingsItems.Add(item);
                }
            }
        }
        
        private string GenerateNewPath(string originalPath)
        {
            var directory = Path.GetDirectoryName(originalPath);
            var originalName = Path.GetFileNameWithoutExtension(originalPath);
            return Path.Combine(directory, $"{originalName}_SheetSync.asset");
        }
        
        private void OnGUI()
        {
            EditorGUILayout.LabelField("KoheiUtils → SheetSync 移行ツール", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            EditorGUILayout.HelpBox("KoheiUtils の ConvertSetting と GlobalCCSettings を SheetSync 版に変換します。\n" +
                                  "変換後のファイルは元のファイル名に '_SheetSync' が付加されます。", MessageType.Info);
            
            EditorGUILayout.Space();
            
            // リフレッシュボタン
            if (GUILayout.Button("アセットリストを更新", GUILayout.Height(25)))
            {
                RefreshAssetList();
            }
            
            EditorGUILayout.Space();
            
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            // ConvertSettings セクション
            showConvertSettings = EditorGUILayout.Foldout(showConvertSettings, $"ConvertSettings ({convertSettingItems.Count} 件)");
            if (showConvertSettings)
            {
                EditorGUI.indentLevel++;
                
                if (convertSettingItems.Count > 0)
                {
                    // 全選択/解除ボタン
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("すべて選択", GUILayout.Width(100)))
                    {
                        convertSettingItems.ForEach(item => { if (!item.alreadyMigrated) item.selected = true; });
                    }
                    if (GUILayout.Button("すべて解除", GUILayout.Width(100)))
                    {
                        convertSettingItems.ForEach(item => item.selected = false);
                    }
                    EditorGUILayout.EndHorizontal();
                    
                    EditorGUILayout.Space();
                    
                    foreach (var item in convertSettingItems)
                    {
                        EditorGUILayout.BeginHorizontal();
                        
                        GUI.enabled = !item.alreadyMigrated;
                        item.selected = EditorGUILayout.Toggle(item.selected, GUILayout.Width(20));
                        GUI.enabled = true;
                        
                        EditorGUILayout.ObjectField(item.originalAsset, typeof(Object), false, GUILayout.Width(200));
                        
                        if (item.alreadyMigrated)
                        {
                            EditorGUILayout.LabelField("移行済み", EditorStyles.miniLabel);
                        }
                        else
                        {
                            EditorGUILayout.LabelField(item.originalPath, EditorStyles.miniLabel);
                        }
                        
                        EditorGUILayout.EndHorizontal();
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("KoheiUtils.ConvertSetting が見つかりません");
                }
                
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.Space();
            
            // GlobalSettings セクション
            showGlobalSettings = EditorGUILayout.Foldout(showGlobalSettings, $"GlobalCCSettings ({globalSettingsItems.Count} 件)");
            if (showGlobalSettings)
            {
                EditorGUI.indentLevel++;
                
                if (globalSettingsItems.Count > 0)
                {
                    foreach (var item in globalSettingsItems)
                    {
                        EditorGUILayout.BeginHorizontal();
                        
                        GUI.enabled = !item.alreadyMigrated;
                        item.selected = EditorGUILayout.Toggle(item.selected, GUILayout.Width(20));
                        GUI.enabled = true;
                        
                        EditorGUILayout.ObjectField(item.originalAsset, typeof(Object), false, GUILayout.Width(200));
                        
                        if (item.alreadyMigrated)
                        {
                            EditorGUILayout.LabelField("移行済み", EditorStyles.miniLabel);
                        }
                        else
                        {
                            EditorGUILayout.LabelField(item.originalPath, EditorStyles.miniLabel);
                        }
                        
                        EditorGUILayout.EndHorizontal();
                    }
                }
                else
                {
                    EditorGUILayout.LabelField("KoheiUtils.GlobalCCSettings が見つかりません");
                }
                
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.EndScrollView();
            
            EditorGUILayout.Space();
            
            // 実行ボタン
            var selectedCount = convertSettingItems.Count(item => item.selected && !item.alreadyMigrated) +
                              globalSettingsItems.Count(item => item.selected && !item.alreadyMigrated);
            
            GUI.enabled = selectedCount > 0;
            if (GUILayout.Button($"選択したアセットを移行 ({selectedCount} 件)", GUILayout.Height(30)))
            {
                MigrateSelectedAssets();
            }
            GUI.enabled = true;
            
            EditorGUILayout.Space();
            
            // 注意事項
            EditorGUILayout.HelpBox("注意: executeAfterImport に KoheiUtils.ConvertSetting への参照がある場合は、" +
                                  "移行後に手動で SheetSync 版への参照に置き換える必要があります。", MessageType.Warning);
        }
        
        private void MigrateSelectedAssets()
        {
            var migratedCount = 0;
            var failedCount = 0;
            
            try
            {
                // ConvertSettings を移行
                foreach (var item in convertSettingItems.Where(i => i.selected && !i.alreadyMigrated))
                {
                    EditorUtility.DisplayProgressBar("移行中", $"移行中: {item.originalAsset.name}", 
                        (float)migratedCount / convertSettingItems.Count(i => i.selected));
                    
                    var success = ConvertSettingMigrationTool.MigrateConvertSettingAsset(item.originalAsset, item.newPath);
                    if (success)
                    {
                        migratedCount++;
                        item.alreadyMigrated = true;
                        item.selected = false;
                    }
                    else
                    {
                        failedCount++;
                    }
                }
                
                // GlobalSettings を移行
                foreach (var item in globalSettingsItems.Where(i => i.selected && !i.alreadyMigrated))
                {
                    EditorUtility.DisplayProgressBar("移行中", $"移行中: {item.originalAsset.name}", 1f);
                    
                    var success = ConvertSettingMigrationTool.MigrateGlobalCCSettingsAsset(item.originalAsset, item.newPath);
                    if (success)
                    {
                        migratedCount++;
                        item.alreadyMigrated = true;
                        item.selected = false;
                    }
                    else
                    {
                        failedCount++;
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            var message = $"移行が完了しました。\n成功: {migratedCount} 件";
            if (failedCount > 0)
            {
                message += $"\n失敗: {failedCount} 件";
            }
            
            EditorUtility.DisplayDialog("移行完了", message, "OK");
        }
    }
}