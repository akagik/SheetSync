using UnityEngine;
using UnityEditor;

namespace SheetSync
{
    /// <summary>
    /// 簡易入力ダイアログ
    /// </summary>
    public class EditorInputDialog : EditorWindow
    {
        private static string inputValue = "";
        private static bool shouldClose = false;
        private static string message = "";
        
        public static string Show(string title, string message, string defaultValue = "")
        {
            inputValue = defaultValue;
            shouldClose = false;
            EditorInputDialog.message = message;
            
            var window = GetWindow<EditorInputDialog>(true, title, true);
            window.minSize = new Vector2(400, 100);
            window.maxSize = new Vector2(400, 100);
            window.ShowModal();
            
            while (!shouldClose)
            {
                System.Threading.Thread.Sleep(50);
            }
            
            return inputValue;
        }
        
        void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField(message);
            EditorGUILayout.Space(5);
            
            inputValue = EditorGUILayout.TextField(inputValue);
            
            EditorGUILayout.Space(10);
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("OK", GUILayout.Width(80)))
            {
                shouldClose = true;
                Close();
            }
            
            if (GUILayout.Button("キャンセル", GUILayout.Width(80)))
            {
                inputValue = "";
                shouldClose = true;
                Close();
            }
            
            EditorGUILayout.EndHorizontal();
        }
        
        void OnDestroy()
        {
            shouldClose = true;
        }
    }
}