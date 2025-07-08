using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace KoheiUtils
{
    /// <summary>
    /// 型情報のキャッシュを管理するクラス
    /// プロジェクト内の型検索を高速化します
    /// </summary>
    public static class CsvReflectionCache
    {
        private static Dictionary<string, List<Type>> _typeNameCache;
        private static Dictionary<string, List<Type>> _fullyQualifiedNameCache;
        private static Dictionary<Type, Dictionary<string, FieldInfo>> _fieldInfoCache;
        private static bool _initialized = false;

        /// <summary>
        /// キャッシュを初期化
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;
            
            _typeNameCache = new Dictionary<string, List<Type>>();
            _fullyQualifiedNameCache = new Dictionary<string, List<Type>>();
            _fieldInfoCache = new Dictionary<Type, Dictionary<string, FieldInfo>>();
            
            // 全アセンブリの型を一度だけ走査してキャッシュ
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (Type type in assembly.GetTypes())
                    {
                        // 型名でキャッシュ
                        if (!_typeNameCache.ContainsKey(type.Name))
                        {
                            _typeNameCache[type.Name] = new List<Type>();
                        }
                        _typeNameCache[type.Name].Add(type);
                        
                        // 完全修飾名でキャッシュ
                        string fullName = type.ToString();
                        if (!_fullyQualifiedNameCache.ContainsKey(fullName))
                        {
                            _fullyQualifiedNameCache[fullName] = new List<Type>();
                        }
                        _fullyQualifiedNameCache[fullName].Add(type);
                    }
                }
                catch (Exception e)
                {
                    // 一部のアセンブリは型の取得に失敗することがあるため、エラーを無視
                    Debug.LogWarning($"Failed to get types from assembly {assembly.FullName}: {e.Message}");
                }
            }
            
            _initialized = true;
        }
        
        /// <summary>
        /// キャッシュをクリア
        /// </summary>
        public static void Clear()
        {
            _typeNameCache?.Clear();
            _fullyQualifiedNameCache?.Clear();
            _fieldInfoCache?.Clear();
            _initialized = false;
        }
        
        /// <summary>
        /// 型名から型のリストを取得（キャッシュ使用）
        /// </summary>
        public static List<Type> GetTypeByName(string name, bool fullyQualifiedName = false)
        {
            Initialize();
            
            if (fullyQualifiedName)
            {
                return _fullyQualifiedNameCache.ContainsKey(name) 
                    ? new List<Type>(_fullyQualifiedNameCache[name]) 
                    : new List<Type>();
            }
            else
            {
                return _typeNameCache.ContainsKey(name) 
                    ? new List<Type>(_typeNameCache[name]) 
                    : new List<Type>();
            }
        }
        
        /// <summary>
        /// フィールド情報を取得（キャッシュ使用）
        /// </summary>
        public static FieldInfo GetFieldInfo(Type type, string fieldName)
        {
            Initialize();
            
            if (!_fieldInfoCache.ContainsKey(type))
            {
                _fieldInfoCache[type] = new Dictionary<string, FieldInfo>();
                
                // 型の全フィールドをキャッシュ
                var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
                foreach (var field in fields)
                {
                    _fieldInfoCache[type][field.Name] = field;
                }
            }
            
            return _fieldInfoCache[type].ContainsKey(fieldName) 
                ? _fieldInfoCache[type][fieldName] 
                : null;
        }
    }
}