using System;
using UnityEngine;
using UnityEditor;
using SheetSync.Editor.ViewModels;

namespace SheetSync.Editor.Windows
{
    /// <summary>
    /// リファクタリングされた SheetSyncWindow
    /// 
    /// MVVM パターンに基づいた SheetSync のメインウィンドウ実装です。
    /// UIロジックのみを担当し、すべてのビジネスロジックは ViewModel に委譲します。
    /// 
    /// 主な責任:
    /// - Unity ImGUI を使用したUIの描画
    /// - ユーザー入力の受け取りとViewModelへの伝達
    /// - ViewModel からのデータ変更通知の受信とUI更新
    /// - Unity エディタのライフサイクルイベントのハンドリング
    /// 
    /// 設計上の特徴:
    /// - View はViewModel のパブリックインターフェースのみに依存
    /// - 将来の UIToolkit への移行が容易な構造
    /// - テスタブルなViewModelとUIの完全な分離
    /// - Unity バージョンに応じたスタイル名の調整
    /// </summary>
    public class SheetSyncWindow : EditorWindow
    {
        private SheetSyncViewModel _viewModel;
        private Vector2 _scrollPosition;
        
        // 検索ボックス用スタイル
        private static GUIStyle _toolbarSearchField;
        private static GUIStyle _toolbarSearchFieldCancelButton;
        private static GUIStyle _toolbarSearchFieldCancelButtonEmpty;
        
        /// <summary>
        /// SheetSync ウィンドウを開きます
        /// </summary>
        [MenuItem("SheetSync/Open SheetSync", false, 0)]
        public static void OpenWindow()
        {
            GetWindow<SheetSyncWindow>(false, "Sheet Sync", true).Show();
        }
        
        private void OnEnable()
        {
            _viewModel = new SheetSyncViewModel();
            _viewModel.PropertyChanged += Repaint;
        }
        
        private void OnDisable()
        {
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= Repaint;
            }
        }
        
        private void OnFocus()
        {
            _viewModel?.RefreshCommand.Execute();
        }
        
        private void OnGUI()
        {
            if (_viewModel == null)
            {
                _viewModel = new SheetSyncViewModel();
                _viewModel.PropertyChanged += Repaint;
            }
            
            GUILayout.Space(6f);
            
            // 検索ボックス
            DrawSearchField();
            
            // メインコンテンツ
            DrawMainContent();
            
            // ボトムバー
            DrawBottomBar();
        }
        
        /// <summary>
        /// 検索フィールドを描画します
        /// </summary>
        /// <remarks>
        /// Unity 標準の検索フィールドスタイルを使用し、
        /// 入力値の変更をViewModelに伝えます。
        /// </remarks>
        private void DrawSearchField()
        {
            GUILayout.BeginHorizontal();
            
            var newSearchText = SearchField(_viewModel.SearchText);
            if (newSearchText != _viewModel.SearchText)
            {
                _viewModel.SearchText = newSearchText;
            }
            
            GUILayout.EndHorizontal();
        }
        
        /// <summary>
        /// メインコンテンツエリアを描画します
        /// </summary>
        /// <remarks>
        /// スクロール可能なエリア内に ConvertSetting のリストを表示します。
        /// 検索テキストによるフィルタリングが適用されます。
        /// </remarks>
        private void DrawMainContent()
        {
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
            
            foreach (var itemViewModel in _viewModel.FilteredItems)
            {
                DrawSettingItem(itemViewModel);
            }
            
            GUILayout.EndScrollView();
        }
        
        /// <summary>
        /// 個別の ConvertSetting アイテムを描画します
        /// </summary>
        /// <param name="itemViewModel">描画するアイテムの ViewModel</param>
        /// <remarks>
        /// 各アイテムはボックス内に表示され、設定に応じて
        /// 異なるボタンやステータスが表示されます。
        /// </remarks>
        private void DrawSettingItem(ConvertSettingItemViewModel itemViewModel)
        {
            GUILayout.BeginHorizontal("box");
            
            #if ODIN_INSPECTOR
            // 複製ボタン
            if (itemViewModel.DuplicateCommand.CanExecute())
            {
                if (GUILayout.Button("+", GUILayout.Width(20)))
                {
                    itemViewModel.DuplicateCommand.Execute();
                    GUIUtility.ExitGUI();
                }
            }
            
            // 編集ボタン
            if (itemViewModel.EditCommand.CanExecute())
            {
                var editIcon = EditorGUIUtility.Load("editicon.sml") as Texture2D;
                if (GUILayout.Button(editIcon, GUILayout.Width(20)))
                {
                    itemViewModel.EditCommand.Execute();
                    GUIUtility.ExitGUI();
                }
            }
            #endif
            
            // 設定名（クリック可能）
            if (GUILayout.Button(itemViewModel.DisplayName, "Label"))
            {
                itemViewModel.PingCommand.Execute();
                GUIUtility.ExitGUI();
            }
            
            // GSPlugin 関連ボタン
            if (itemViewModel.Model.UseGSPlugin)
            {
                DrawGSPluginButtons(itemViewModel);
            }
            
            // Verbose モードボタン
            if (itemViewModel.Model.IsVerboseMode)
            {
                DrawVerboseButtons(itemViewModel);
            }
            
            // 出力参照
            DrawOutputReference(itemViewModel);
            
            GUILayout.EndHorizontal();
            
            // ステータスメッセージ
            if (!string.IsNullOrEmpty(itemViewModel.StatusMessage))
            {
                EditorGUILayout.HelpBox(itemViewModel.StatusMessage, MessageType.Info);
            }
        }
        
        /// <summary>
        /// GSPlugin 関連のボタンを描画します
        /// </summary>
        /// <param name="itemViewModel">対象アイテムの ViewModel</param>
        /// <remarks>
        /// Import、Open、Download などの Google スプレッドシート
        /// 関連の操作ボタンを表示します。
        /// </remarks>
        private void DrawGSPluginButtons(ConvertSettingItemViewModel itemViewModel)
        {
            GUI.enabled = !itemViewModel.IsProcessing && !_viewModel.IsProcessing;
            
            if (GUILayout.Button("Import", GUILayout.Width(110)))
            {
                _viewModel.ExecuteImport(itemViewModel);
                GUIUtility.ExitGUI();
            }
            
            if (itemViewModel.OpenSpreadsheetCommand.CanExecute())
            {
                if (GUILayout.Button("Open", GUILayout.Width(80)))
                {
                    itemViewModel.OpenSpreadsheetCommand.Execute();
                    GUIUtility.ExitGUI();
                }
            }
            
            if (itemViewModel.Model.IsVerboseMode)
            {
                if (GUILayout.Button("Download", GUILayout.Width(110)))
                {
                    _viewModel.ExecuteDownload(itemViewModel);
                    GUIUtility.ExitGUI();
                }
            }
            
            GUI.enabled = true;
        }
        
        /// <summary>
        /// Verbose モード用のボタンを描画します
        /// </summary>
        /// <param name="itemViewModel">対象アイテムの ViewModel</param>
        /// <remarks>
        /// コード生成やアセット作成などの詳細モード用
        /// 操作ボタンを表示します。
        /// </remarks>
        private void DrawVerboseButtons(ConvertSettingItemViewModel itemViewModel)
        {
            GUI.enabled = itemViewModel.Model.CanGenerateCode && !itemViewModel.IsProcessing && !_viewModel.IsProcessing;
            
            if (GUILayout.Button("Generate Code", GUILayout.Width(110)))
            {
                _viewModel.GenerateCode(itemViewModel);
                GUIUtility.ExitGUI();
            }
            
            GUI.enabled = itemViewModel.Model.CanCreateAsset && !itemViewModel.IsProcessing && !_viewModel.IsProcessing;
            
            if (GUILayout.Button("Create Assets", GUILayout.Width(110)))
            {
                _viewModel.CreateAssets(itemViewModel);
                GUIUtility.ExitGUI();
            }
            
            GUI.enabled = true;
        }
        
        /// <summary>
        /// 出力先アセットの参照フィールドを描画します
        /// </summary>
        /// <param name="itemViewModel">対象アイテムの ViewModel</param>
        /// <remarks>
        /// 読み取り専用の ObjectField として表示し、
        /// 生成されたアセットへの参照を表示します。
        /// </remarks>
        private void DrawOutputReference(ConvertSettingItemViewModel itemViewModel)
        {
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.ObjectField(
                itemViewModel.Model.OutputReference, 
                typeof(UnityEngine.Object), 
                false, 
                GUILayout.Width(100)
            );
            EditorGUI.EndDisabledGroup();
        }
        
        /// <summary>
        /// ウィンドウ下部のボタンバーを描画します
        /// </summary>
        /// <remarks>
        /// すべての ConvertSetting に対して一括操作を行う
        /// グローバルアクションボタンを表示します。
        /// </remarks>
        private void DrawBottomBar()
        {
            GUILayout.BeginHorizontal("box");
            
            GUI.enabled = _viewModel.GenerateAllCodeCommand.CanExecute();
            if (GUILayout.Button("Generate All Codes", "LargeButtonMid"))
            {
                _viewModel.GenerateAllCodeCommand.Execute();
                GUIUtility.ExitGUI();
            }
            
            GUI.enabled = _viewModel.CreateAllAssetsCommand.CanExecute();
            if (GUILayout.Button("Create All Assets", "LargeButtonMid"))
            {
                _viewModel.CreateAllAssetsCommand.Execute();
                GUIUtility.ExitGUI();
            }
            
            GUI.enabled = true;
            
            GUILayout.EndHorizontal();
        }
        
        #region Search Field Implementation
        
        /// <summary>
        /// 検索フィールドの実装
        /// </summary>
        /// <remarks>
        /// Unity 標準のツールバー検索フィールドスタイルを再現し、
        /// クリアボタンやプレースホルダーなどの機能を提供します。
        /// Unity バージョンによるスタイル名の違いにも対応しています。
        /// </remarks>
        
        /// <summary>
        /// 検索フィールドをレンダリングします
        /// </summary>
        /// <param name="text">現在の検索テキスト</param>
        /// <returns>更新された検索テキスト</returns>
        private string SearchField(string text)
        {
            Rect rect = GUILayoutUtility.GetRect(16f, 24f, 16f, 24f, new GUILayoutOption[]
            {
                GUILayout.Width(400f)
            });
            rect.x += 4f;
            rect.y += 4f;
            
            return ToolbarSearchField(rect, text);
        }
        
        /// <summary>
        /// ツールバースタイルの検索フィールドを描画します
        /// </summary>
        /// <param name="position">描画位置</param>
        /// <param name="text">現在のテキスト</param>
        /// <returns>更新されたテキスト</returns>
        private string ToolbarSearchField(Rect position, string text)
        {
            InitializeSearchFieldStyles();
            
            Rect rect = position;
            rect.x += position.width;
            rect.width = 14f;
            
            text = EditorGUI.TextField(position, text, _toolbarSearchField);
            
            if (string.IsNullOrEmpty(text))
            {
                GUI.Button(rect, GUIContent.none, _toolbarSearchFieldCancelButtonEmpty);
            }
            else
            {
                if (GUI.Button(rect, GUIContent.none, _toolbarSearchFieldCancelButton))
                {
                    text = "";
                    GUIUtility.keyboardControl = 0;
                }
            }
            
            return text;
        }
        
        /// <summary>
        /// 検索フィールドのスタイルを初期化します
        /// </summary>
        /// <remarks>
        /// Unity 2022.1 以降でのスタイル名変更に対応しています。
        /// </remarks>
        private void InitializeSearchFieldStyles()
        {
            if (_toolbarSearchField == null)
            {
                #if UNITY_2022_1_OR_NEWER
                _toolbarSearchField = GetStyle("ToolbarSearchTextField");
                #else
                _toolbarSearchField = GetStyle("ToolbarSeachTextField");
                #endif
            }
            
            if (_toolbarSearchFieldCancelButtonEmpty == null)
            {
                #if UNITY_2022_1_OR_NEWER
                _toolbarSearchFieldCancelButtonEmpty = GetStyle("ToolbarSearchCancelButtonEmpty");
                #else
                _toolbarSearchFieldCancelButtonEmpty = GetStyle("ToolbarSeachCancelButtonEmpty");
                #endif
            }
            
            if (_toolbarSearchFieldCancelButton == null)
            {
                #if UNITY_2022_1_OR_NEWER
                _toolbarSearchFieldCancelButton = GetStyle("ToolbarSearchCancelButton");
                #else
                _toolbarSearchFieldCancelButton = GetStyle("ToolbarSeachCancelButton");
                #endif
            }
        }
        
        /// <summary>
        /// 指定名の GUIStyle を取得します
        /// </summary>
        /// <param name="styleName">スタイル名</param>
        /// <returns>見つかった GUIStyle、または空の GUIStyle</returns>
        /// <remarks>
        /// Unity の組み込みスキンからスタイルを検索し、
        /// 見つからない場合はエラーログを出力して空のスタイルを返します。
        /// </remarks>
        private static GUIStyle GetStyle(string styleName)
        {
            GUIStyle gUIStyle = GUI.skin.FindStyle(styleName);
            if (gUIStyle == null)
            {
                gUIStyle = EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector).FindStyle(styleName);
            }
            
            if (gUIStyle == null)
            {
                Debug.LogError("Missing built-in guistyle " + styleName);
                gUIStyle = new GUIStyle();
            }
            
            return gUIStyle;
        }
        
        #endregion
    }
}