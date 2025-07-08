using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Object = UnityEngine.Object;

namespace KoheiUtils
{
    public class AssetsGenerator
    {
        private ConvertSetting setting;
        private Type[] customAssetTypes;
        private HashSet<Type> customAssetTypeSet;

        private Field[] fields;
        private CsvData content;

        // setup 情報
        private Type assetType;
        public string dstFolder { get; private set; }
        private int[] keyIndexes;

        // テーブル情報
        // これらの情報は setting.tableGenerate が true のときのみ利用される.
        private Type tableType;
        public ScriptableObject tableInstance;
        private object dataList = null;
        
        // Join 情報
        private Type targetTableRowType;
        FieldInfo levelListInfo;
        FieldInfo targetTableKeyFieldInfo;

        // ログ情報
        public Result result;

        public class Result
        {
            public int createdRowCount;
            public Object tableInstance;
        }

        public int contentRowCount => content.row;

        public AssetsGenerator(ConvertSetting _setting, Field[] _fields, CsvData _content)
        {
            setting = _setting;
            fields = _fields;
            content = _content;
        }

        public void SetCustomAssetTypes(Type[] _customAssetTypes)
        {
            customAssetTypes = _customAssetTypes;
            customAssetTypeSet = new HashSet<Type>(_customAssetTypes);
        }

        public void Setup(Type _assetType, string settingPath)
        {
            result = new Result();

            assetType = _assetType;
            dstFolder = CCLogic.GetFilePathRelativesToAssets(settingPath, setting.destination);

            // Asset の名前をつけるときに利用する key.
            keyIndexes = ClassGenerator.FindKeyIndexes(setting, fields);
        }

        // テーブルありの設定
        public void Setup(Type _assetType, Type _tableType, string settingPath)
        {
            tableType = _tableType;
            Setup(_assetType, settingPath);

            FieldInfo dataListField = tableType.GetField(ClassGenerator.ROWS,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);

            // 既存のテーブルインスタンスがストレージにあればロードし、なければ新規に作成する.
            string filePath = Path.Combine(dstFolder, setting.tableAssetName + ".asset");
            tableInstance = AssetDatabase.LoadAssetAtPath<ScriptableObject>(filePath);
            if (tableInstance == null)
            {
                tableInstance = ScriptableObject.CreateInstance(tableType);
                AssetDatabase.CreateAsset(tableInstance, filePath);
            }
            result.tableInstance = tableInstance;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            dataList = dataListField.GetValue(tableInstance);

            // 初めてテーブルを作成する場合は null になっているので、
            // インスタンスを作成して、tableInstance に代入する。
            if (dataList == null)
            {
                dataList = CreateListInstance(dataListField.FieldType.GenericTypeArguments[0]);
                dataListField.SetValue(tableInstance, dataList);
            }

            dataList.GetType().GetMethod("Clear").Invoke(dataList, null);
        }

        public bool LoadJoinTable(Object tableObject, string targetFieldName, string targetKeyFieldName)
        {
            tableType = tableObject.GetType();

            this.targetTableKeyFieldInfo = tableType.GetField(targetKeyFieldName);
            FieldInfo dataListField = tableType.GetField(ClassGenerator.ROWS,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);

            tableInstance = tableObject as ScriptableObject;

            if (tableInstance == null)
            {
                return false;
            }
            dataList = dataListField.GetValue(tableInstance);
            
            if (dataList == null)
            {
                return false;
            }

            this.targetTableRowType = dataList.GetType().GetGenericArguments()[0];
            IEnumerable collection = dataList as IEnumerable;
            this.levelListInfo = targetTableRowType.GetField(targetFieldName);
            
            foreach (var row in collection)
            {
                var levelList = levelListInfo.GetValue(row);

                if (levelList == null)
                {
                    levelList = CreateListInstance(assetType);
                    levelListInfo.SetValue(row, levelList);
                }
                else
                {
                    levelList.GetType().GetMethod("Clear").Invoke(levelList, null);
                }
            }
            
            return true;
        }

        private object CreateListInstance(Type elementType)
        {
            // 初めてテーブルを作成する場合は null になっているので、
            // インスタンスを作成して、tableInstance に代入する。
            Type listType = typeof(List<>);
            var constructedListType = listType.MakeGenericType(elementType);
            return Activator.CreateInstance(constructedListType);
        }

        private string createAssetName(int rowIndex)
        {
            string fileName = "";

            // キーがある場合は、キーの値をアセット名に含める.
            if (keyIndexes.Length > 0)
            {
                fileName = setting.className;
                for (int j = 0; j < keyIndexes.Length; j++)
                {
                    int keyIndex = keyIndexes[j];
                    fileName += "_" + content.Get(rowIndex, keyIndex).Trim();
                }

                fileName += ".asset";
            }
            else
            {
                fileName = setting.className + rowIndex + ".asset";
            }

            return fileName;
        }

        private bool checkKeyIsValid(int rowIndex)
        {
            for (int j = 0; j < keyIndexes.Length; j++)
            {
                int keyIndex = keyIndexes[j];
                string key = content.Get(rowIndex, keyIndex).Trim();

                if (key == "")
                {
                    return false;
                }
            }

            return true;
        }

        [Flags]
        public enum ResultType
        {
            None = 0,
            SkipNoKey = 1 << 0,
            EmptyCell = 1 << 1,
            ConvertFails = 1 << 2,
            JoinIndexMismatch = 1 << 3,
            JoinNoReferenceRow = 1 << 4,
            JoinNoFindMethod = 1 << 5,
            VersionMismatch = 1 << 6,
            
            All = SkipNoKey | EmptyCell | ConvertFails | JoinIndexMismatch | JoinNoReferenceRow | JoinNoFindMethod | VersionMismatch
        }

        public ResultType CreateCsvAssetAt(int i, GlobalCCSettings gSettings)
        {
            ResultType resultType = ResultType.None;
            
            int line = i + 2 + 1;

            if (!checkKeyIsValid(i))
            {
                return ResultType.SkipNoKey;
            }

            string fileName = createAssetName(i);
            string filePath = Path.Combine(dstFolder, fileName);

            object data = null;

            // テーブルのみ作成する場合は ScriptableObject としてではなく
            // 通常のインスタンスとして作成する.
            if (setting.IsPureClass)
            {
                data = Activator.CreateInstance(assetType);
            }
            else
            {
                data = AssetDatabase.LoadAssetAtPath(filePath, assetType);
                if (data == null)
                {
                    data = ScriptableObject.CreateInstance(assetType);
                    AssetDatabase.CreateAsset(data as UnityEngine.Object, filePath);
                    Debug.LogFormat("Create \"{0}\"", filePath);
                }
                else
                {
                    Debug.LogFormat("Update \"{0}\"", filePath);
                }
            }

            // フィールド名に[]が付いているカラムを先に検索し、配列インスタンスを生成しておく.
            for (int j = 0; j < content.col; j++)
            {
                if (!fields[j].isValid) continue;

                FieldInfo info = CsvReflectionCache.GetFieldInfo(assetType, fields[j].fieldNameWithoutIndexing);

                // フィールド名が配列要素の場合は配列のデータをセットしておく.
                if (fields[j].isArrayField)
                {
                    Type elementType = info.FieldType.GetElementType();

                    if (elementType != null)
                    {
                        int length = 0;
                        var v = Array.CreateInstance(elementType, length);
                        info.SetValue(data, v);
                    }
                    else
                    {
                        Debug.LogError("不正な配列フィールドの型です:" + info.FieldType);
                        fields[j].isValid = false;
                    }
                }
            }

            // version チェック
            if (!string.IsNullOrWhiteSpace(gSettings.versionFieldName))
            {
                for (int j = 0; j < content.col; j++)
                {
                    if (fields[j].isVersion)
                    {
                        string sValue = content.Get(i, j);

                        if (!string.IsNullOrWhiteSpace(sValue))
                        {
                            sValue = "\"" + sValue + "\"";
                            string value = (string) Str2TypeConverter.Convert(typeof(string), sValue);

                            if (!Version.TryParse(value, out Version version))
                            {
                                Debug.LogErrorFormat("{0}行{1}列目: 不正なバージョン文字列: \"{2}\"", line, j + 1, value);
                                return ResultType.ConvertFails;
                            }
                            Version appVersion = new Version(Application.version);

                            if (version.CompareTo(appVersion) > 0)
                            {
                                return ResultType.VersionMismatch;
                            }
                        }
                        break;
                    }
                }
            }

            // 各列に対して、有効なフィールドのみ値を読み込んで実際のデータに変換し、この行のインスタンス data に代入する.
            for (int j = 0; j < content.col; j++)
            {
                if (!fields[j].isValid) continue;

                FieldInfo info = CsvReflectionCache.GetFieldInfo(assetType, fields[j].fieldNameWithoutIndexing);
                Type fieldType = fields[j].GetTypeAs(info);

                // (i, j) セルに格納されている生のテキストデータを fieldType 型に変換する.
                object value = null;
                {
                    string sValue = content.Get(i, j);

                    // 文字列型のときは " でラップする.
                    if (fieldType == typeof(string))
                    {
                        sValue = "\"" + sValue + "\"";
                    }

                    if (sValue == "")
                    {
                        if ((gSettings.logType & ResultType.EmptyCell) != 0)
                        {
                            Debug.LogWarningFormat("{0} {1}行{2}列目: 空の値があります: {3}=\"{4}\"", setting.className, line,
                                j + 1, info.Name, sValue);
                        }
                    }
                    else
                    {
                        value = Str2TypeConverter.Convert(fieldType, sValue);

                        // 基本型で変換できないときは GlobalSettings の customAssetTypes で変換を試みる.
                        if (value == null && customAssetTypeSet != null && customAssetTypeSet.Contains(fieldType))
                        {
                            value = Str2TypeConverter.LoadAsset(sValue, fieldType);
                        }

                        if (value == null)
                        {
                            if ((gSettings.logType & ResultType.ConvertFails) != 0)
                            {
                                Debug.LogErrorFormat("{0} {1}行{2}列目: 変換に失敗しました: {3}=\"{4}\"", setting.className, line,
                                    j + 1, info.Name, sValue);
                            }
                        }
                    }
                }

                // フィールド名が配列要素の場合
                // もともとの配列データを読み込んで、そこに value を追加した配列を value とする.
                // TODO 添字を反映させる.
                if (fields[j].isArrayField)
                {
                    var t = ((IEnumerable) info.GetValue(data));

                    Type listType = typeof(List<>);
                    var constructedListType = listType.MakeGenericType(info.FieldType.GetElementType());
                    var objects = Activator.CreateInstance(constructedListType);

                    IEnumerable<object> infoValue = ((IEnumerable) info.GetValue(data)).Cast<object>();

                    if (infoValue != null)
                    {
                        for (int k = 0; k < infoValue.Count(); k++)
                        {
                            object obj = infoValue.ElementAt(k);
                            objects.GetType().GetMethod("Add").Invoke(objects, new object[] {obj});
                        }
                    }

                    objects.GetType().GetMethod("Add").Invoke(objects, new object[] {value});
                    value = objects.GetType().GetMethod("ToArray").Invoke(objects, new object[] { });
                }
                
                info.SetValue(data, value);
            }

            if (!setting.IsPureClass)
            {
                EditorUtility.SetDirty(data as UnityEngine.Object);
            }
            
            if (setting.tableGenerate)
            {
                dataList.GetType().GetMethod("Add").Invoke(dataList, new object[] {data});
            }
            else if (setting.join)
            {
                object keyValue = data.GetType().GetField(setting.selfJoinKeyField).GetValue(data);
                var findMethod = tableType.GetMethod(setting.targetFindMethodName);

                if (findMethod == null)
                {
                    return ResultType.JoinNoFindMethod;
                }
                
                object row = findMethod.Invoke(tableInstance, new[] { keyValue });

                if (row == null)
                {
                    resultType |= ResultType.JoinNoReferenceRow;
                    return resultType;
                }
                
                object levels = levelListInfo.GetValue(row);
                var addMethod = levels.GetType().GetMethod("Add");
                addMethod.Invoke(levels, new[] { data });

//                int currentCount = (int) levels.GetType().GetProperty("Count").GetValue(levels);
//                int index = (int) data.GetType().GetField(setting.joinIndexField).GetValue(data);
//
//                if (currentCount - 1 != index)
//                {
//                    Debug.Log($"Join {keyValue} <- {index} (current: {currentCount})");
//                    resultType &= ResultType.JoinIndexMismatch;
//                }
            }

            result.createdRowCount++;
            return resultType;
        }
    }
}