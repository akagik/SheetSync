using System;
using System.Collections.Generic;
using UnityEngine;

namespace SheetSync
{
    /// <summary>
    /// Enum型への変換処理を責任を持つヘルパークラス
    /// </summary>
    public static class EnumConverter
    {
        /// <summary>
        /// 文字列をEnum値に変換する
        /// "EnumType.Value" 形式の文字列を受け入れ、型名からEnum型を検索して変換する
        /// Flags属性を持つEnumの場合は、ビット演算（|）もサポートする
        /// </summary>
        /// <param name="value">変換元の文字列</param>
        /// <returns>変換されたEnum値（int型）、変換できない場合はnull</returns>
        public static object ParseEnumString(string value)
        {
            // "EnumType.Value" 形式の解析
            string[] splits = value.Split('.');
            
            if (splits.Length != 2)
            {
                return null;
            }
            
            string typeName = splits[0];
            string enumValue = splits[1];
            
            // 型名からEnum型を検索
            List<Type> candidates = CCLogic.GetTypeByName(typeName);
            
            if (candidates.Count == 0)
            {
                Debug.LogWarningFormat("指定のenum型 '{0}' が見つかりませんでした", typeName);
                return null;
            }
            
            if (candidates.Count > 1)
            {
                Debug.LogWarningFormat("指定のenum型 '{0}' が複数見つかりました（{1}個）。最初の型を使用します。", typeName, candidates.Count);
            }
            
            Type enumType = candidates[0];
            
            if (!enumType.IsEnum)
            {
                Debug.LogWarningFormat("指定の型 '{0}' はEnum型ではありません", typeName);
                return null;
            }
            
            // Enum値の解析
            try
            {
                // Flags属性を持つEnumの場合、ビット演算をサポート
                if (enumType.IsDefined(typeof(FlagsAttribute), false))
                {
                    return ParseFlagsEnum(enumType, enumValue);
                }
                else
                {
                    object enumObject = Enum.Parse(enumType, enumValue);
                    return Convert.ToInt32(enumObject);
                }
            }
            catch (ArgumentException)
            {
                Debug.LogWarningFormat("Enum型 '{0}' に値 '{1}' が存在しません", typeName, enumValue);
                return null;
            }
        }
        
        /// <summary>
        /// 直接Enum型が指定されている場合の変換処理
        /// </summary>
        /// <param name="enumType">対象のEnum型</param>
        /// <param name="value">変換元の文字列</param>
        /// <returns>変換されたEnum値、変換できない場合はnull</returns>
        public static object ParseEnum(Type enumType, string value)
        {
            if (!enumType.IsEnum)
            {
                throw new ArgumentException("指定された型はEnum型ではありません", nameof(enumType));
            }
            
            try
            {
                // Flags属性を持つEnumの場合、ビット演算をサポート
                if (enumType.IsDefined(typeof(FlagsAttribute), false))
                {
                    return ParseFlagsEnum(enumType, value);
                }
                else
                {
                    return Enum.Parse(enumType, value);
                }
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
        
        /// <summary>
        /// Flags属性を持つEnumの解析
        /// "Value1 | Value2" のような形式をサポート
        /// </summary>
        private static object ParseFlagsEnum(Type enumType, string value)
        {
            // "|" で分割して各値を解析
            string[] parts = value.Split('|');
            int result = 0;
            
            foreach (string part in parts)
            {
                string trimmedPart = part.Trim();
                if (string.IsNullOrEmpty(trimmedPart))
                    continue;
                    
                object parsedValue = Enum.Parse(enumType, trimmedPart);
                result |= (int)parsedValue;
            }
            
            return result;
        }
    }
}