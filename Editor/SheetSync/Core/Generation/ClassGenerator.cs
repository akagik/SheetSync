using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using KoheiUtils;
using SheetSync;

namespace SheetSync
{
    public class ClassGenerator
    {
        const string FIELD_FORMAT = "    public {0} {1};\n";
        public static readonly string ROWS = "rows";
        
        // パッケージ名を定数として定義
        private const string PACKAGE_NAME = "com.kc-works.sheet-sync";
        private const string TEMPLATE_BASE_PATH = "Editor/SheetSync/Core/Generation/Templates/Templates/";

        public static string GenerateClass(string name, Field[] fields, bool isPureClass)
        {
            string classData = "";
            classData = "using UnityEngine;\n";
            classData += "using System.Collections.Generic;\n";
            classData += "\n";

            if (isPureClass)
            {
                classData += "[System.Serializable]\n";
                classData += "public class " + name + "\n";
            }
            else
            {
                classData += "public class " + name + " : ScriptableObject\n";
            }

            classData += "{\n";

            HashSet<string> addedFields = new HashSet<string>();
            for (int col = 0; col < fields.Length; col++)
            {
                Field f = fields[col];
                if (addedFields.Contains(f.fieldNameWithoutIndexing)) continue;

                string fieldName = f.fieldName;
                string typeName = f.typeName;

                if (fieldName == "" || typeName == "")
                {
                    continue;
                }

                if (f.isArrayField)
                {
                    fieldName = f.fieldNameWithoutIndexing;
                    typeName = typeName + "[]";
                }

                classData += string.Format(FIELD_FORMAT, typeName, fieldName);
                addedFields.Add(f.fieldNameWithoutIndexing);
            }

            classData += "}\n";

            return classData;
        }

        public static string GenerateTableClass(SheetSync.ConvertSetting setting, string tableClassName, Field[] keys)
        {
            string className = setting.className;

            string code = "";

            if (setting.isDictionary)
            {
                code = LoadDictTableTemplate();

                if (keys == null)
                {
                    throw new Exception("Dictionary Table にはキーが必要です");
                }

                if (keys.Length != 1)
                {
                    throw new Exception("Dictionary Table はキーを１つだけ指定してください");
                }

                code = code.Replace("%KeyType%", keys[0].typeName);
                code = code.Replace("%KeyName%", keys[0].fieldName);
            }
            // キーが有効な場合はキーから検索できるようにする
            else if (keys != null && keys.All((arg) => arg.isValid))
            {
                code = LoadListTableTemplate();

                string argStr = "";
                string condStr = "";

                for (int i = 0; i < keys.Length; i++)
                {
                    argStr += string.Format("{0} {1}, ", keys[i].typeName, keys[i].fieldName);
                    condStr += string.Format("o.{0} == {0} && ", keys[i].fieldName);
                }

                argStr = argStr.Substring(0, argStr.Length - 2);
                condStr = condStr.Substring(0, condStr.Length - 4);
                code = code.Replace("%FindArguments%", argStr);
                code = code.Replace("%FindPredicate%", condStr);
            }
            // キーなしリストテーブル
            else
            {
                code = LoadNoKeyListTableTemplate();
            }

            code = code.Replace("%TableClassName%", tableClassName);
            code = code.Replace("%ClassName%", className);

            return code;
        }

        public static string LoadNoKeyListTableTemplate()
        {
            return LoadTemplateFile("template_nokey_list_table.txt");
        }

        public static string LoadListTableTemplate()
        {
            return LoadTemplateFile("template_list_table.txt");
        }

        public static string LoadDictTableTemplate()
        {
            return LoadTemplateFile("template_dict_table.txt");
        }
        
        /// <summary>
        /// テンプレートファイルを読み込む共通メソッド
        /// </summary>
        private static string LoadTemplateFile(string fileName)
        {
            // まずパッケージパスで試す
            string packagePath = $"Packages/{PACKAGE_NAME}/{TEMPLATE_BASE_PATH}{fileName}";
            TextAsset ta = AssetDatabase.LoadAssetAtPath<TextAsset>(packagePath);
            
            if (ta != null)
            {
                return ta.text;
            }
            
            // フォールバック: 現在のスクリプトの位置から相対パスで探す
            var scriptPath = GetScriptPath();
            if (!string.IsNullOrEmpty(scriptPath))
            {
                var templateDir = Path.GetDirectoryName(scriptPath);
                templateDir = Path.Combine(templateDir, "Templates/Templates");
                var relativePath = Path.Combine(templateDir, fileName).Replace('\\', '/');
                
                // Assetsからの相対パスに変換
                if (relativePath.Contains("/Packages/"))
                {
                    int packageIndex = relativePath.IndexOf("/Packages/");
                    relativePath = relativePath.Substring(packageIndex + 1);
                }
                
                ta = AssetDatabase.LoadAssetAtPath<TextAsset>(relativePath);
                if (ta != null)
                {
                    return ta.text;
                }
            }
            
            Debug.LogError($"Template not found: {fileName}\nTried paths:\n- {packagePath}\n- {scriptPath}");
            return string.Empty;
        }
        
        /// <summary>
        /// このスクリプトファイルのパスを取得（より簡単な方法）
        /// </summary>
        private static string GetScriptPath()
        {
            // ClassGeneratorスクリプト自体を検索
            var guids = AssetDatabase.FindAssets("t:Script ClassGenerator");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("ClassGenerator.cs"))
                {
                    return path;
                }
            }
            return null;
        }

        public static int[] FindKeyIndexes(SheetSync.ConvertSetting setting, Field[] fields)
        {
            List<int> indexes = new List<int>();

            string[] keys = setting.keys;
            // Debug.Log(keys.ToString<string>());

            for (int j = 0; j < keys.Length; j++)
            {
                for (int i = 0; i < fields.Length; i++)
                {
                    if (fields[i].fieldName == keys[j])
                    {
                        indexes.Add(i);
                    }
                }
            }

            return indexes.ToArray();
        }
    }
}
