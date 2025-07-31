using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using KoheiUtils;

namespace SheetSync.Services
{
    /// <summary>
    /// コード生成サービスの実装
    /// </summary>
    public class CodeGenerationServiceImpl : ICodeGenerationService
    {
        public void GenerateCode(ConvertSetting setting, GlobalCCSettings globalSettings)
        {
            CodeGenerationService.GenerateCode(setting, globalSettings);
        }

        public void GenerateCodeFromData(ConvertSetting setting, GlobalCCSettings globalSettings, ICsvData csvData, string directoryPath)
        {
            CodeGenerationService.GenerateCodeFromData(setting, globalSettings, csvData, directoryPath);
        }

        public object CreateAssets(ConvertSetting setting, GlobalCCSettings globalSettings)
        {
            return CodeGenerationService.CreateAssets(setting, globalSettings);
        }

        public object CreateAssets(ConvertSetting setting, GlobalCCSettings globalSettings, ICsvDataProvider dataProvider)
        {
            return CodeGenerationService.CreateAssets(setting, globalSettings, dataProvider);
        }
    }
}