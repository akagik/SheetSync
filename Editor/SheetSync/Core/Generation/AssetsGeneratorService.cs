using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SheetSync
{
    /// <summary>
    /// AssetsGeneratorの値変換処理を責任を持つサービスクラス
    /// </summary>
    public class AssetsGeneratorService
    {
        private readonly HashSet<Type> customAssetTypeSet;

        public AssetsGeneratorService(Type[] customAssetTypes = null)
        {
            customAssetTypeSet = customAssetTypes != null ? new HashSet<Type>(customAssetTypes) : null;
        }

        /// <summary>
        /// CSVの文字列値を指定された型に変換する
        /// </summary>
        /// <param name="fieldType">変換先の型</param>
        /// <param name="csvValue">CSV内の文字列値</param>
        /// <returns>変換された値、変換できない場合はnull</returns>
        public object ConvertCsvValueToFieldType(Type fieldType, string csvValue)
        {
            string sValue = csvValue;

            // 文字列型のときは " でラップする
            if (fieldType == typeof(string))
            {
                sValue = "\"" + sValue + "\"";
            }

            if (string.IsNullOrEmpty(sValue))
            {
                return null;
            }

            // 基本的な型変換を試みる
            object value = Str2TypeConverter.Convert(fieldType, sValue);

            // 基本型で変換できないときは customAssetTypes で変換を試みる
            if (value == null && customAssetTypeSet != null && customAssetTypeSet.Contains(fieldType))
            {
                value = Str2TypeConverter.LoadAsset(sValue, fieldType);
            }

            return value;
        }

        /// <summary>
        /// フィールドに値を設定する
        /// 配列フィールドの場合は既存の値に追加する
        /// </summary>
        /// <param name="targetObject">設定先のオブジェクト</param>
        /// <param name="fieldInfo">フィールド情報</param>
        /// <param name="value">設定する値</param>
        /// <param name="isArrayField">配列フィールドかどうか</param>
        public void SetFieldValue(object targetObject, FieldInfo fieldInfo, object value, bool isArrayField)
        {
            if (isArrayField)
            {
                value = AppendToArrayField(targetObject, fieldInfo, value);
            }

            // enum型フィールドの場合、int値をenum型に変換する
            if (fieldInfo.FieldType.IsEnum && value != null && value.GetType() == typeof(int))
            {
                value = Enum.ToObject(fieldInfo.FieldType, value);
            }

            fieldInfo.SetValue(targetObject, value);
        }

        /// <summary>
        /// 配列フィールドに値を追加する
        /// </summary>
        private object AppendToArrayField(object targetObject, FieldInfo fieldInfo, object newValue)
        {
            Type listType = typeof(List<>);
            var constructedListType = listType.MakeGenericType(fieldInfo.FieldType.GetElementType());
            var list = Activator.CreateInstance(constructedListType);

            IEnumerable<object> currentValues = ((IEnumerable)fieldInfo.GetValue(targetObject))?.Cast<object>();

            if (currentValues != null)
            {
                foreach (object obj in currentValues)
                {
                    list.GetType().GetMethod("Add").Invoke(list, new object[] { obj });
                }
            }

            list.GetType().GetMethod("Add").Invoke(list, new object[] { newValue });
            return list.GetType().GetMethod("ToArray").Invoke(list, new object[] { });
        }

        /// <summary>
        /// 配列フィールドを初期化する
        /// </summary>
        public void InitializeArrayField(object targetObject, FieldInfo fieldInfo)
        {
            Type elementType = fieldInfo.FieldType.GetElementType();
            if (elementType != null)
            {
                var emptyArray = Array.CreateInstance(elementType, 0);
                fieldInfo.SetValue(targetObject, emptyArray);
            }
        }
    }
}