using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SheetSync.Migration
{
    /// <summary>
    /// SheetSyncのフォルダ構造を再編成するための移行ユーティリティ
    /// </summary>
    public static class RestructureMigration
    {
        private const string ROOT_PATH = "Packages/SheetSync/Editor/SheetSync";
        
        // ファイル移動マッピング
        private static readonly Dictionary<string, string> FileMappings = new Dictionary<string, string>
        {
            // Phase 2: Models の統合
            ["Data/Models/ConvertSetting.cs"] = "Data/Models/ConvertSetting.cs",
            ["Data/Models/Field.cs"] = "Data/Models/Field.cs",
            ["Data/Models/GlobalCCSettings.cs"] = "Data/Models/GlobalSettings.cs", // 名前変更
            ["Data/Models/ResultType.cs"] = "Data/Models/ResultType.cs",
            ["Models/ConvertSettingItem.cs"] = "Data/Models/ConvertSettingItem.cs",
            ["Models/SheetSyncRepository.cs"] = "Data/Implementations/SheetRepository.cs",
            
            // Phase 3: Core 層の再編成
            ["Core/Converters/Str2TypeConverter.cs"] = "Core/Conversion/TypeConverter.cs",
            ["Core/Importers/CsvConvert.cs"] = "Core/Conversion/CsvConverter.cs",
            ["Core/Importers/CreateAssetsJob.cs"] = "Core/Import/ImportJob.cs",
            ["Core/Generators/ClassGenerator.cs"] = "Core/Generation/ClassGenerator.cs",
            ["Core/Generators/EnumGenerator.cs"] = "Core/Generation/EnumGenerator.cs",
            ["Core/Generators/AssetsGenerator.cs"] = "Core/Generation/AssetsGenerator.cs",
            ["Templates"] = "Core/Generation/Templates", // フォルダごと移動
            
            // Phase 4: UI 層の整理
            ["Windows/SheetSyncWindow.cs"] = "UI/Windows/SheetSyncWindow.cs",
            ["Windows/CCSettingsEditWindow.cs"] = "UI/Windows/SettingsWindow.cs",
            ["ViewModels"] = "UI/ViewModels", // フォルダごと移動
            ["Commands/ICommand.cs"] = "UI/Commands/ICommand.cs",
            
            // Phase 5: Utilities の統合
            ["Utils/EditorCoroutineRunner.cs"] = "Utilities/EditorCoroutineRunner.cs",
            ["Utils/EditorInputDialog.cs"] = "Utilities/EditorInputDialog.cs",
            ["Utils/GoogleApiChecker.cs"] = "Utilities/GoogleApiChecker.cs",
            ["Utils/McpConnectionHelper.cs"] = "Utilities/McpConnectionHelper.cs",
            ["Infrastructure/Utilities/CCLogic.cs"] = "Utilities/PathUtility.cs", // 名前変更
            
            // Data層の整理
            ["Data/ICsvData.cs"] = "Data/Interfaces/ICsvData.cs",
            ["Data/ICsvDataProvider.cs"] = "Data/Interfaces/ICsvDataProvider.cs",
            ["Data/SheetData.cs"] = "Data/Implementations/SheetData.cs",
            ["Data/Runtime/CsvData.cs"] = "Data/Implementations/CsvData.cs",
            ["Data/Providers"] = "Data/Providers", // フォルダごと移動
            
            // Infrastructure層の整理
            ["Infrastructure/Csv/CsvLogic.cs"] = "Infrastructure/Csv/CsvParser.cs",
            ["Infrastructure/Reflection/CsvReflectionCache.cs"] = "Infrastructure/Reflection/ReflectionCache.cs",
            
            // Services層
            ["Services/GoogleSheetsDownloader.cs"] = "Services/GoogleSheetsService.cs",
            ["Services/SheetSyncService.cs"] = "Services/SheetSyncService.cs",
        };
        
        [MenuItem("Tools/SheetSync/Migration/Phase 1 - Preview Changes")]
        public static void PreviewMigration()
        {
            Debug.Log("=== SheetSync Folder Restructure Migration Preview ===");
            
            foreach (var mapping in FileMappings)
            {
                string sourcePath = Path.Combine(ROOT_PATH, mapping.Key);
                string destPath = Path.Combine(ROOT_PATH, mapping.Value);
                
                if (File.Exists(sourcePath) || Directory.Exists(sourcePath))
                {
                    Debug.Log($"MOVE: {mapping.Key} → {mapping.Value}");
                }
                else
                {
                    Debug.LogWarning($"NOT FOUND: {mapping.Key}");
                }
            }
            
            Debug.Log("=== Preview Complete ===");
        }
        
        [MenuItem("Tools/SheetSync/Migration/Execute Phase")]
        public static void ShowPhaseDialog()
        {
            var window = ScriptableObject.CreateInstance<MigrationPhaseWindow>();
            window.titleContent = new GUIContent("Select Migration Phase");
            window.ShowUtility();
        }
        
        public static void ExecutePhase(int phase)
        {
            Debug.Log($"=== Executing Phase {phase} ===");
            
            var phaseFiles = GetFilesForPhase(phase);
            int successCount = 0;
            int errorCount = 0;
            
            foreach (var mapping in phaseFiles)
            {
                if (MoveFileOrDirectory(mapping.Key, mapping.Value))
                {
                    successCount++;
                }
                else
                {
                    errorCount++;
                }
            }
            
            AssetDatabase.Refresh();
            
            Debug.Log($"=== Phase {phase} Complete: {successCount} successful, {errorCount} errors ===");
        }
        
        private static Dictionary<string, string> GetFilesForPhase(int phase)
        {
            var result = new Dictionary<string, string>();
            
            switch (phase)
            {
                case 2: // Models の統合
                    result = FileMappings.Where(kvp => 
                        kvp.Key.Contains("Models") || 
                        kvp.Value.Contains("Data/Models")).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                    break;
                    
                case 3: // Core 層の再編成
                    result = FileMappings.Where(kvp => 
                        kvp.Key.StartsWith("Core/") || 
                        kvp.Value.StartsWith("Core/")).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                    break;
                    
                case 4: // UI 層の整理
                    result = FileMappings.Where(kvp => 
                        kvp.Key.StartsWith("Windows/") || 
                        kvp.Key.StartsWith("ViewModels/") ||
                        kvp.Key.StartsWith("Commands/") ||
                        kvp.Value.StartsWith("UI/")).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                    break;
                    
                case 5: // Utilities の統合
                    result = FileMappings.Where(kvp => 
                        kvp.Key.StartsWith("Utils/") || 
                        kvp.Key.Contains("Utilities") ||
                        kvp.Value.StartsWith("Utilities/")).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                    break;
            }
            
            return result;
        }
        
        private static bool MoveFileOrDirectory(string relativeSource, string relativeDest)
        {
            string sourcePath = Path.Combine(ROOT_PATH, relativeSource);
            string destPath = Path.Combine(ROOT_PATH, relativeDest);
            
            try
            {
                // ディレクトリの場合
                if (Directory.Exists(sourcePath))
                {
                    string destDir = Path.GetDirectoryName(destPath);
                    if (!Directory.Exists(destDir))
                    {
                        Directory.CreateDirectory(destDir);
                    }
                    
                    // git mv コマンドを使用
                    string gitCommand = $"cd {Application.dataPath}/../../ && git mv {sourcePath} {destPath}";
                    Debug.Log($"Executing: {gitCommand}");
                    
                    return true;
                }
                // ファイルの場合
                else if (File.Exists(sourcePath))
                {
                    string destDir = Path.GetDirectoryName(destPath);
                    if (!Directory.Exists(destDir))
                    {
                        Directory.CreateDirectory(destDir);
                    }
                    
                    // メタファイルも一緒に移動
                    string metaSource = sourcePath + ".meta";
                    string metaDest = destPath + ".meta";
                    
                    File.Move(sourcePath, destPath);
                    if (File.Exists(metaSource))
                    {
                        File.Move(metaSource, metaDest);
                    }
                    
                    Debug.Log($"Moved: {relativeSource} → {relativeDest}");
                    return true;
                }
                else
                {
                    Debug.LogWarning($"Source not found: {relativeSource}");
                    return false;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error moving {relativeSource}: {e.Message}");
                return false;
            }
        }
    }
    
    public class MigrationPhaseWindow : EditorWindow
    {
        private void OnGUI()
        {
            GUILayout.Label("Select Migration Phase", EditorStyles.boldLabel);
            
            if (GUILayout.Button("Phase 2: Models の統合"))
            {
                RestructureMigration.ExecutePhase(2);
                Close();
            }
            
            if (GUILayout.Button("Phase 3: Core 層の再編成"))
            {
                RestructureMigration.ExecutePhase(3);
                Close();
            }
            
            if (GUILayout.Button("Phase 4: UI 層の整理"))
            {
                RestructureMigration.ExecutePhase(4);
                Close();
            }
            
            if (GUILayout.Button("Phase 5: Utilities の統合"))
            {
                RestructureMigration.ExecutePhase(5);
                Close();
            }
        }
    }
}