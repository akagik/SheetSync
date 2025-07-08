using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEditor;
#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif

namespace KoheiUtils
{
    [Serializable]
    [CreateAssetMenu(menuName = "KoheiUtils/CsvConverter/ConverterSettings")]
    public class ConvertSetting : ScriptableObject
    {
        public ConvertSetting Copy()
        {
            var copied = ScriptableObject.CreateInstance<ConvertSetting>();
            
            copied.csvFilePath = this.csvFilePath;
            copied.className = this.className;
            copied.checkFullyQualifiedName = this.checkFullyQualifiedName;
            copied.destination = this.destination;
            copied.codeDestination = this.codeDestination;
            copied.isEnum = this.isEnum;
            copied.classGenerate = this.classGenerate;

            // table
            copied.tableGenerate = this.tableGenerate;
            copied.tableClassName = this.tableClassName;
            copied._tableAssetName = this._tableAssetName;
            copied.tableClassGenerate = this.tableClassGenerate;
            copied.isDictionary = this.isDictionary;
            copied.onlyTableCreate = this.onlyTableCreate;

            // join
            copied.join = this.join;
            copied.targetTable = this.targetTable;
            copied.targetJoinKeyField = this.targetJoinKeyField;
            copied.selfJoinKeyField = this.selfJoinKeyField;
            copied.targetJoinListField = this.targetJoinListField;
            copied.targetFindMethodName = this.targetFindMethodName;

            copied.key = this.key;
            copied.executeAfterImport = new List<ConvertSetting>(executeAfterImport);
            copied.executeMethodAfterImport= new List<string>(executeMethodAfterImport);
            copied.executeValidationAfterImport = new List<string>(executeValidationAfterImport);
            copied.useGSPlugin = this.useGSPlugin;
            copied.sheetID = this.sheetID;
            copied.gid = this.gid;
            copied.tempCsvPath = this.tempCsvPath;
            copied.verbose = this.verbose;
            copied.verboseBtn = this.verboseBtn;

            return copied;
        }

#if ODIN_INSPECTOR
        [Title("Basic Settings")]
#endif
#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.HideIf("tempCsvPath")]
#endif
        [Tooltip("For example, \"../tmp/test.csv\"")]
        public string csvFilePath;

        [Required]
        public string className;

        [Tooltip("Check a class name by fully qualified name")]
        public bool checkFullyQualifiedName;

        [Required]
        public string destination = ".";
        
        [ShowIf("@classGenerate || tableClassGenerate || isEnum")]
        public string codeDestination = ".";

#if ODIN_INSPECTOR
        [Title("Advanced Settings")]
#endif
        public bool isEnum;

#if ODIN_INSPECTOR
        [HideIf("isEnum")]
#endif
        public bool classGenerate;

        /// ----------------------------------------------------
        /// テーブル設定.
        /// ----------------------------------------------------
#if ODIN_INSPECTOR
        [HideIf("isEnum")] [HideIf("join")] [ValidateInput(condition: "@!(this.join && this.tableGenerate)")]
#endif
        public bool tableGenerate;

#if ODIN_INSPECTOR
        [ShowIf("tableGenerate")]
        [Title("TableGenerate")]
        [InfoBox("If empty string, its value is \"{ClassName}Table\".")]
#endif
        [SerializeField]
        public string tableClassName;

#if ODIN_INSPECTOR
        [ShowIf("tableGenerate")] [InfoBox("If empty string, its value is tableClassName.")]
#endif
        public string _tableAssetName;

#if ODIN_INSPECTOR
        [ShowIf("tableGenerate")]
#endif
        public bool tableClassGenerate;

#if ODIN_INSPECTOR
        [ShowIf("tableGenerate")]
#endif
        public bool isDictionary;


#if ODIN_INSPECTOR
        [ShowIf("tableGenerate")]
#endif
        public bool onlyTableCreate = true;

        /// ----------------------------------------------------
        /// Join List 関連.
        /// ----------------------------------------------------
#if ODIN_INSPECTOR
        [HideIf("tableGenerate")] [ValidateInput(condition: "@!(this.join && this.tableGenerate)")]
#endif
        public bool join;

#if ODIN_INSPECTOR
        [ShowIf("join")]
        [Title("Join")]
        [Required]
#endif
        [SerializeField]
        [Tooltip("Table of join destination.")]
        public UnityEngine.Object targetTable;

#if ODIN_INSPECTOR
        [ShowIf("join")]
        [Required]
#endif
        [SerializeField]
        [Tooltip("Name of the key field of the class of the table to be joined.")]
        public string targetJoinKeyField;

#if ODIN_INSPECTOR
        [ShowIf("join")]
        [Required]
#endif
        [SerializeField]
        [Tooltip("Name of its own key field.")]
        public string selfJoinKeyField;

#if ODIN_INSPECTOR
        [ShowIf("join")]
        [Required]
#endif
        [SerializeField]
        [Tooltip("The name of the list field that the class of the table to be joined has. Each element of this list corresponds to a row of itself.")]
        public string targetJoinListField;

#if ODIN_INSPECTOR
        [ShowIf("join")]
        [Required]
#endif
        [SerializeField]
        [Tooltip("The name of a find method that takes the key of the table class to be joined as an argument.")]
        public string targetFindMethodName;

        /// ----------------------------------------------------
        /// その他
        /// ----------------------------------------------------
#if ODIN_INSPECTOR
        [Title("Others")] [HideIf("isEnum")]
#endif
        public string key; // ScriptableObject の名前に使用.

        public string[] keys
        {
            get
            {
                if (key == null || key.Length == 0)
                {
                    return new string[0];
                }

                return key.Split(',').Select((arg) => arg.Trim()).Where((arg) => arg.Length > 0).ToArray();
            }
        }
        
        [Tooltip("Import by these settings after importing.")]
        public List<ConvertSetting> executeAfterImport;
        
        [Tooltip("Import 後に実行する static メソッド. NameSpace.TypeName.MethodName(, AssemblyName)の形で指定する")]
        public List<string> executeMethodAfterImport;
        
        [Tooltip("table を引数に受け取り、 Validation が成功した場合は true を返すような static メソッドを指定する.")]
        public List<string> executeValidationAfterImport;

        public bool useGSPlugin = true;

#if ODIN_INSPECTOR
        [Title("GoogleSpreadSheet Settings")] [Sirenix.OdinInspector.ShowIf("useGSPlugin")]
        [Required]
#endif
        public string sheetID;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.ShowIf("useGSPlugin")]
        [Required]
#endif
        public string gid;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.ShowIf("useGSPlugin")]
#endif
        [Tooltip("中間出力される csv ファイルのパスを Global Settings で指定された一時パスを使うようにする.")]
        public bool tempCsvPath = true;

        [Title("Debug")] public bool verbose;
        public bool verboseBtn;

        // code を生成できるか？
        public bool canGenerateCode
        {
            get { return isEnum || classGenerate || tableGenerate; }
        }

        // asset を生成できるかどうか?
        public bool canCreateAsset
        {
            get { return !isEnum; }
        }

        public string TableClassName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(tableClassName))
                {
                    return className + "Table";
                }

                return tableClassName;
            }
        }

        public string tableAssetName
        {
            get
            {
                if (_tableAssetName.Length > 0)
                {
                    return _tableAssetName;
                }

                return TableClassName;
            }
        }

        // この設定で生成される行データを ScriptableObject でなく、ピュアクラスのインスタンスとして扱うか？
        public bool IsPureClass => (tableGenerate && onlyTableCreate) || join;

        public string GetCsvPath(GlobalCCSettings gSettings)
        {
            if (useGSPlugin && tempCsvPath)
            {
                return gSettings.tempCsvPath;
            }

            return csvFilePath;
        }

        public string GetDirectoryPath()
        {
            string assetPath = AssetDatabase.GetAssetPath(this);
            return System.IO.Path.GetDirectoryName(assetPath);
        }

#if ODIN_INSPECTOR && UNITY_EDITOR
        private bool IsValidClassName(string name)
        {
            if (name == null)
            {
                return false;
            }

            return name.Length > 0 && char.IsUpper(name[0]);
        }
#endif
    }
}