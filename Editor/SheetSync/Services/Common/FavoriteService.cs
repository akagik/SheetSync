using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace SheetSync
{
    /// <summary>
    /// お気に入り設定を EditorPrefs に永続化する静的サービス。
    /// お気に入りに登録された AssetPath をパイプ区切りで保存します。
    /// </summary>
    public static class FavoriteService
    {
        private const string PrefsKey = "SheetSync_Favorites";
        private const char Separator = '|';

        public static HashSet<string> GetFavorites()
        {
            var raw = EditorPrefs.GetString(PrefsKey, "");
            if (string.IsNullOrEmpty(raw))
                return new HashSet<string>();

            return new HashSet<string>(raw.Split(Separator));
        }

        public static bool IsFavorite(string assetPath)
        {
            return GetFavorites().Contains(assetPath);
        }

        public static void ToggleFavorite(string assetPath)
        {
            var favorites = GetFavorites();

            if (!favorites.Remove(assetPath))
                favorites.Add(assetPath);

            Save(favorites);
        }

        private static void Save(HashSet<string> favorites)
        {
            EditorPrefs.SetString(PrefsKey, string.Join(Separator.ToString(), favorites));
        }
    }
}
