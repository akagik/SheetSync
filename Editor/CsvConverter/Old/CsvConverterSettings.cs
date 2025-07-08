namespace KoheiUtils
{
    using System;
    using System.IO;
    using UnityEngine;
    using UnityEditor;
    using System.Linq;
#if ODIN_INSPECTOR
    using Sirenix.OdinInspector;

#endif

    public class CsvConverterSettings : ScriptableObject
    {
        public Setting[] list;

        [Serializable]
        public class Setting
        {
            public Setting()
            {
                
            }

            public Setting(Setting orig)
            {
                this.csvFilePath = orig.csvFilePath;
                this.className = orig.className;
                this.checkFullyQualifiedName = orig.checkFullyQualifiedName;
                this.destination = orig.destination;
                this.codeDestination = orig.codeDestination;
                this.isEnum = orig.isEnum;
                this.classGenerate = orig.classGenerate;
                
                // table
                this.tableGenerate = orig.tableGenerate;
                this.tableClassName = orig.tableClassName;
                this._tableAssetName = orig._tableAssetName;
                this.tableClassGenerate = orig.tableClassGenerate;
                this.isDictionary = orig.isDictionary;
                this.onlyTableCreate = orig.onlyTableCreate;
                
                // join
                this.join = orig.join;
                this.targetTable = orig.targetTable;
                this.targetJoinKeyField = orig.targetJoinKeyField;
                this.selfJoinKeyField = orig.selfJoinKeyField;
                this.targetJoinListField = orig.targetJoinListField;
                this.targetFindMethodName = orig.targetFindMethodName;
                
                this.key = orig.key;
                this.useGSPlugin = orig.useGSPlugin;
                this.sheetID = orig.sheetID;
                this.gid = orig.gid;
                this.tempCsvPath = orig.tempCsvPath;
                this.verbose = orig.verbose;
                this.verboseBtn = orig.verboseBtn;
            }

            public ConvertSetting ToNewSettings()
            {
                var obj = ScriptableObject.CreateInstance<ConvertSetting>();
                
                obj.csvFilePath = this.csvFilePath;
                obj.className = this.className;
                obj.checkFullyQualifiedName = this.checkFullyQualifiedName;
                obj.destination = this.destination;
                obj.codeDestination = this.codeDestination;
                obj.isEnum = this.isEnum;
                obj.classGenerate = this.classGenerate;
                
                // table
                obj.tableGenerate = this.tableGenerate;
                obj.tableClassName = this.tableClassName;
                obj._tableAssetName = this._tableAssetName;
                obj.tableClassGenerate = this.tableClassGenerate;
                obj.isDictionary = this.isDictionary;
                obj.onlyTableCreate = this.onlyTableCreate;
                
                // join
                obj.join = this.join;
                obj.targetTable = this.targetTable;
                obj.targetJoinKeyField = this.targetJoinKeyField;
                obj.selfJoinKeyField = this.selfJoinKeyField;
                obj.targetJoinListField = this.targetJoinListField;
                obj.targetFindMethodName = this.targetFindMethodName;
                
                obj.key = this.key;
                obj.useGSPlugin = this.useGSPlugin;
                obj.sheetID = this.sheetID;
                obj.gid = this.gid;
                obj.tempCsvPath = this.tempCsvPath;
                obj.verbose = this.verbose;
                obj.verboseBtn = this.verboseBtn;

                return obj;
            }

#if ODIN_INSPECTOR
            [Title("Basic Settings")]
#endif
#if ODIN_INSPECTOR
            [Sirenix.OdinInspector.HideIf("tempCsvPath")]
#endif
            [Tooltip("For example, \"../tmp/test.csv\"")]
            public string csvFilePath;

            public string className;

            [Tooltip("Check a class name by fully qualified name")]
            public bool checkFullyQualifiedName;

            public string destination     = "";
            public string codeDestination = "";

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
            [HideIf("isEnum")]
            [HideIf("join")]
            [ValidateInput(condition:"@!(this.join && this.tableGenerate)")]
#endif
            public bool tableGenerate;

#if ODIN_INSPECTOR
            [ShowIf("tableGenerate")]
            [Title("TableGenerate")]
            [InfoBox("If empty string, its value is \"{ClassName}Table\".")]
#endif
            [SerializeField]
            string tableClassName;

#if ODIN_INSPECTOR
            [ShowIf("tableGenerate")]
            [InfoBox("If empty string, its value is tableClassName.")]
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
            public bool onlyTableCreate;
            
            /// ----------------------------------------------------
            /// Join List 関連.
            /// ----------------------------------------------------
#if ODIN_INSPECTOR
            [HideIf("tableGenerate")]
            [ValidateInput(condition:"@!(this.join && this.tableGenerate)")]
#endif
            public bool join;

#if ODIN_INSPECTOR
            [ShowIf("join")]
            [Title("Join")]
            [Required]
#endif
            [SerializeField]
            public UnityEngine.Object targetTable;
            
#if ODIN_INSPECTOR
            [ShowIf("join")]
            [Required]
#endif
            [SerializeField]
            public string targetJoinKeyField;
            
#if ODIN_INSPECTOR
            [ShowIf("join")]
            [Required]
#endif
            [SerializeField]
            public string selfJoinKeyField;
            
#if ODIN_INSPECTOR
            [ShowIf("join")]
            [Required]
#endif
            [SerializeField]
            public string targetJoinListField;
            
#if ODIN_INSPECTOR
            [ShowIf("join")]
            [Required]
#endif
            [SerializeField]
            public string targetFindMethodName;

            /// ----------------------------------------------------
            /// その他
            /// ----------------------------------------------------
#if ODIN_INSPECTOR
            [Title("Others")]
            [HideIf("isEnum")]
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


            public bool useGSPlugin;

#if ODIN_INSPECTOR
            [Title("GSPlugin")]
            [Sirenix.OdinInspector.ShowIf("useGSPlugin")]
#endif
            public string sheetID;

#if ODIN_INSPECTOR
            [Sirenix.OdinInspector.ShowIf("useGSPlugin")]
#endif
            public string gid;

#if ODIN_INSPECTOR
            [Sirenix.OdinInspector.ShowIf("useGSPlugin")]
#endif
            [Tooltip("中間出力される csv ファイルのパスを Global Settings で指定された一時パスを使うようにする.")]
            public bool tempCsvPath;

            [Title("Debug")]
            public bool verbose;
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
}