using System;
using UnityEngine;
using UnityEditor;
using SheetSync;
using KoheiUtils;

namespace SheetSync
{
    /// <summary>
    /// 個別の ConvertSetting 用ビューモデル
    /// 
    /// 単一の ConvertSetting インスタンスに対するビジネスロジックとUIコマンドを管理します。
    /// SheetSyncViewModel の一部として動作し、各設定項目の独立した操作を可能にします。
    /// 
    /// 主な責任:
    /// - ConvertSetting の状態管理と表示用データの提供
    /// - 個別アクション（編集、複製、インポートなど）のコマンド実装
    /// - 処理状態とステータスメッセージの管理
    /// - Odin Inspector の有無に応じた機能の有効/無効化
    /// 
    /// 設計上の特徴:
    /// - ConvertSettingItem モデルをラップし、UIに必要な機能を追加
    /// - コマンドパターンによる操作の実装
    /// - 条件付きコンパイルによる Odin Inspector 依存機能の制御
    /// </summary>
    public class ConvertSettingItemViewModel : ViewModelBase
    {
        private readonly ConvertSettingItem _model;
        private bool _isProcessing;
        private string _statusMessage;
        
        public ConvertSettingItem Model => _model;
        public string DisplayName => _model.DisplayName;
        public string AssetPath => _model.AssetPath;
        
        public bool IsProcessing
        {
            get => _isProcessing;
            set => SetProperty(ref _isProcessing, value);
        }
        
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }
        
        // Commands
        public ICommand PingCommand { get; }
        public ICommand DuplicateCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand OpenSpreadsheetCommand { get; }
        
        /// <summary>
        /// ConvertSettingItemViewModel を初期化します
        /// </summary>
        /// <param name="model">ラップする ConvertSettingItem モデル</param>
        /// <exception cref="ArgumentNullException">model が null の場合</exception>
        public ConvertSettingItemViewModel(ConvertSettingItem model)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            
            // Initialize commands
            PingCommand = new RelayCommand(Ping);
            DuplicateCommand = new RelayCommand(Duplicate, CanDuplicate);
            EditCommand = new RelayCommand(Edit, CanEdit);
            OpenSpreadsheetCommand = new RelayCommand(OpenSpreadsheet, CanOpenSpreadsheet);
        }
        
        /// <summary>
        /// Unity エディタ上で対象の ConvertSetting アセットをハイライトします
        /// </summary>
        private void Ping()
        {
            EditorGUIUtility.PingObject(_model.Settings);
        }
        
        /// <summary>
        /// 複製コマンドが実行可能かどうかを判定します
        /// </summary>
        /// <returns>Odin Inspector が有効な場合は true、それ以外は false</returns>
        private bool CanDuplicate()
        {
            #if ODIN_INSPECTOR
            return true;
            #else
            return false;
            #endif
        }
        
        /// <summary>
        /// ConvertSetting を複製し、編集ウィンドウで開きます
        /// </summary>
        /// <remarks>
        /// Odin Inspector が必要な機能です。
        /// 複製された設定は元の設定と同じディレクトリに配置されます。
        /// </remarks>
        private void Duplicate()
        {
            #if ODIN_INSPECTOR
            var copied = _model.Settings.Copy();
            var window = SheetSync.CCSettingsEditWindow.OpenWindow();
            window.SetNewSettings(copied, _model.Settings.GetDirectoryPath());
            #endif
        }
        
        /// <summary>
        /// 編集コマンドが実行可能かどうかを判定します
        /// </summary>
        /// <returns>Odin Inspector が有効な場合は true、それ以外は false</returns>
        private bool CanEdit()
        {
            #if ODIN_INSPECTOR
            return true;
            #else
            return false;
            #endif
        }
        
        /// <summary>
        /// ConvertSetting の編集ウィンドウを開きます
        /// </summary>
        /// <remarks>
        /// Odin Inspector が必要な機能です。
        /// CCSettingsEditWindow を使用して詳細な設定編集が可能です。
        /// </remarks>
        private void Edit()
        {
            #if ODIN_INSPECTOR
            var window = SheetSync.CCSettingsEditWindow.OpenWindow();
            window.SetSettings(_model.Settings);
            #endif
        }
        
        /// <summary>
        /// スプレッドシートを開くコマンドが実行可能かどうかを判定します
        /// </summary>
        /// <returns>GSPlugin が有効で処理中でない場合は true</returns>
        private bool CanOpenSpreadsheet()
        {
            return _model.UseGSPlugin && !_isProcessing;
        }
        
        /// <summary>
        /// 関連付けられた Google スプレッドシートをブラウザで開きます
        /// </summary>
        /// <remarks>
        /// GSPlugin を使用してスプレッドシートの URL を生成し、開きます。
        /// </remarks>
        private void OpenSpreadsheet()
        {
            KoheiUtils.GSUtils.OpenURL(_model.Settings.sheetID, _model.Settings.gid);
        }
        
        /// <summary>
        /// ステータスメッセージと処理状態を更新します
        /// </summary>
        /// <param name="message">表示するステータスメッセージ</param>
        /// <param name="isProcessing">処理中かどうかのフラグ（デフォルト: false）</param>
        /// <remarks>
        /// この更新により PropertyChanged イベントが発火し、UIが自動的に更新されます。
        /// </remarks>
        public void UpdateStatus(string message, bool isProcessing = false)
        {
            StatusMessage = message;
            IsProcessing = isProcessing;
        }
    }
}
