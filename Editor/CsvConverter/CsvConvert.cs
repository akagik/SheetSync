namespace KoheiUtils
{
    using System;
    using System.IO;
    using System.Reflection;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEditor;
    using ResultType = AssetsGenerator.ResultType;

    public class CsvConvert
    {
        public static void GenerateCode(ConvertSetting s, GlobalCCSettings gSettings)
        {
            string settingPath = s.GetDirectoryPath();
            string    csvPath   = CCLogic.GetFilePathRelativesToAssets(settingPath, s.GetCsvPath(gSettings));
            TextAsset textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(csvPath);

            if (textAsset == null)
            {
                Debug.LogError("Not found : " + csvPath);
                return;
            }

            string directoryPath = CCLogic.GetFullPath(settingPath, s.codeDestination);

            if (!Directory.Exists(directoryPath))
            {
                Debug.LogError("Not found directory: " + directoryPath);
                return;
            }

            CsvData csv = CsvLogic.GetValidCsvData(textAsset.text, gSettings);

            if (s.isEnum)
            {
                CsvData headers  = csv.Slice(gSettings.rowIndexOfName, gSettings.rowIndexOfName + 1);
                CsvData contents = csv.Slice(gSettings.rowIndexOfEnumContentStart);
                string  code     = EnumGenerator.Generate(s.className, headers, contents, s.verbose);

                string filePath = Path.Combine(directoryPath, s.className + ".cs");
                using (StreamWriter writer = File.CreateText(filePath))
                {
                    writer.WriteLine(code);
                }

                Debug.LogFormat("Create \"{0}\"", filePath);
            }
            else
            {
                Field[] fields = CsvLogic.GetFieldsFromHeader(csv, gSettings);

                if (s.classGenerate)
                {
                    string code = ClassGenerator.GenerateClass(s.className, fields, s.IsPureClass);

                    string filePath = Path.Combine(directoryPath, s.className + ".cs");
                    using (StreamWriter writer = File.CreateText(filePath))
                    {
                        writer.WriteLine(code);
                    }

                    Debug.LogFormat("Create \"{0}\"", filePath);
                }

                if (s.tableClassGenerate)
                {
                    int[] keyIndexes = ClassGenerator.FindKeyIndexes(s, fields);

                    string[] keys = s.keys;
                    Field[]  key  = null;
                    if (keyIndexes.Length > 0)
                    {
                        List<Field> keyFieldList = new List<Field>();

                        for (int i = 0; i < keyIndexes.Length; i++)
                        {
                            keyFieldList.Add(fields[keyIndexes[i]]);
                        }

                        key = keyFieldList.ToArray();
                    }

                    string code = ClassGenerator.GenerateTableClass(s, s.TableClassName, key);

                    string filePath = Path.Combine(directoryPath, s.TableClassName + ".cs");
                    using (StreamWriter writer = File.CreateText(filePath))
                    {
                        writer.WriteLine(code);
                    }

                    Debug.LogFormat("Create \"{0}\"", filePath);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static object CreateAssets(ConvertSetting s, GlobalCCSettings gSettings)
        {
            string settingPath = s.GetDirectoryPath();
            string    csvPath   = CCLogic.GetFilePathRelativesToAssets(settingPath, s.GetCsvPath(gSettings));
            TextAsset textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(csvPath);

            if (textAsset == null)
            {
                Debug.LogError("Not found : " + csvPath);
                return null;
            }

            if (s.isEnum)
            {
                return null;
            }

            // csv ファイルから読み込み
            CsvData csv      = CsvLogic.GetValidCsvData(textAsset.text, gSettings);
            CsvData contents = csv.Slice(gSettings.rowIndexOfContentStart);

            Field[] fields = CsvLogic.GetFieldsFromHeader(csv, gSettings);

            // アセットを生成する.
            AssetsGenerator assetsGenerator = new AssetsGenerator(s, fields, contents);

            // カスタムアセットタイプを設定する
            // これはプロジェクト固有のアセットをテーブルでセット出来るようにする.
            {
                Type[] customAssetTypes = new Type[gSettings.customAssetTypes.Length];
                for (int i = 0; i < customAssetTypes.Length; i++)
                {
                    if (TryGetTypeWithError(gSettings.customAssetTypes[i], out var type, s.checkFullyQualifiedName))
                    {
                        customAssetTypes[i] = type;
                    }
                    else
                    {
                        return null;
                    }
                }

                assetsGenerator.SetCustomAssetTypes(customAssetTypes);
            }

            Type assetType;
            if (!TryGetTypeWithError(s.className, out assetType, s.checkFullyQualifiedName))
            {
                return null;
            }

            // 生成する各要素の class type を取得
            // class のフィールド名と一致しないものは除外する.
            for (int j = 0; j < fields.Length; j++)
            {
                if (!fields[j].isValid || fields[j].isVersion)
                {
                    continue;
                }

                // フィールド名が配列表の場合は [] の部分を削除する
                // 例) names[2] => names
                string    fieldName = fields[j].fieldNameWithoutIndexing;
                FieldInfo info      = CsvReflectionCache.GetFieldInfo(assetType, fieldName);

                if (info == null)
                {
                    Debug.LogWarningFormat("{0} に存在しないフィールド \"{1}\" を無視", s.className, fieldName);
                    fields[j].isValid = false;
                }
            }

            // テーブルを生成する場合は、生成するテーブル class type を取得
            Type tableType = null;
            if (s.tableGenerate)
            {
                if (!TryGetTypeWithError(s.TableClassName, out tableType, s.checkFullyQualifiedName))
                {
                    return null;
                }
            }

            if (s.tableGenerate)
            {
                assetsGenerator.Setup(assetType, tableType, settingPath);
            }
            else
            {
                assetsGenerator.Setup(assetType, settingPath);
            }

            if (s.join)
            {
                bool isSuccess = assetsGenerator.LoadJoinTable(s.targetTable, s.targetJoinListField, s.targetJoinKeyField);

                if (!isSuccess)
                {
                    Debug.LogError("不正な Join 設定です");
                    return null;
                }
            }

            // アセットを作成する.
            for (int i = 0; i < assetsGenerator.contentRowCount; i++)
            {
                int        line       = i + 2 + 1;
                ResultType resultType = assetsGenerator.CreateCsvAssetAt(i, gSettings);

                if ((resultType & ResultType.SkipNoKey & gSettings.logType) != 0)
                {
                    Debug.LogWarningFormat("{0} line {1}: key が存在しない行をスキップしました", s.className, line);
                }
                if ((resultType & ResultType.JoinIndexMismatch & gSettings.logType) != 0)
                {
                    Debug.LogErrorFormat("{0} line {1}: Join する index の値が不正です. index は 0 から始めて連続する整数である必要があります", s.className, line);
                }
                if ((resultType & ResultType.JoinNoReferenceRow & gSettings.logType) != 0)
                {
                    Debug.LogErrorFormat("{0} line {1}: Join の対象となるマスターが存在しません", s.className, line);
                }
                if ((resultType & ResultType.JoinNoFindMethod & gSettings.logType) != 0)
                {
                    Debug.LogErrorFormat("{0} line {1}: Join Target のテーブル class に指定された Find メソッドが存在しません: {2}", s.className, line, s.targetFindMethodName);
                }
                if ((resultType & ResultType.VersionMismatch & gSettings.logType) != 0)
                {
                    Debug.LogWarningFormat("{0} line {1}: version のミスマッチ行をスキップ.", s.className, line);
                }

                int total = assetsGenerator.contentRowCount;
                if (total <= 10 || i == total - 1 || i % (total / 10) == 0)
                {
                    float progress = (float) i / total;
                    EditorUtility.DisplayProgressBar("Progress", $"Creating {s.className} ({i + 1}/{total})", progress);
                }
            }

            // 結果を出力して保存する.
            var result = assetsGenerator.result;
            if (s.tableGenerate)
            {
                EditorUtility.SetDirty(result.tableInstance);
                Debug.Log($"Create \"{Path.Combine(assetsGenerator.dstFolder, s.tableAssetName)}.asset\"");
            }
            else if (s.@join)
            {
                EditorUtility.SetDirty(assetsGenerator.tableInstance);
                Debug.Log($"Join \"{Path.Combine(assetsGenerator.dstFolder, s.tableAssetName)}.asset\"");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"生成された総行数: {result.createdRowCount}");

            if (result.tableInstance != null)
            {
                EditorGUIUtility.PingObject(result.tableInstance.GetInstanceID());
            }

            EditorUtility.ClearProgressBar();
            
            if (s.tableGenerate)
            {
                return result.tableInstance;
            }
            else if (s.@join)
            {
                return assetsGenerator.tableInstance;
            }

            return null;
        }

        public static bool TryGetTypeWithError(string name, out Type type, bool fullyQualifiedName = false,
            bool                                      dialog = true)
        {
            List<Type> candidates = CCLogic.GetTypeByName(name, fullyQualifiedName);
            type = null;

            if (candidates.Count == 0)
            {
                if (dialog)
                    EditorUtility.DisplayDialog(
                        "Error",
                        "Cannot find the class \"" + name + "\", please execute \"Tools/CsvConverter/Generate Code\".",
                        "ok"
                    );
                return false;
            }

            if (candidates.Count > 1)
            {
                if (dialog)
                {
                    string msg = "複数候補の class が発見されました: \"" + name + "\".";
                    msg += candidates.ToString<Type>();
                    EditorUtility.DisplayDialog(
                        "Error",
                        msg,
                        "ok"
                    );
                }

                return false;
            }

            type = candidates[0];
            return true;
        }
    }
}