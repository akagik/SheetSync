using System;
using UnityEngine;

#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif

namespace SheetSync
{
#if ODIN_INSPECTOR
    /// <summary>
    /// Google SpreadsheetsのURL入力ヘルパーボタンを表示するための属性
    /// ConvertSettingクラスのフィールドに適用して使用
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class GoogleSheetsUrlHelperAttribute : Attribute
    {
        public string ButtonLabel { get; set; } = "URL Helper";
        
        public GoogleSheetsUrlHelperAttribute() { }
        
        public GoogleSheetsUrlHelperAttribute(string buttonLabel)
        {
            ButtonLabel = buttonLabel;
        }
    }
#endif
}