using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using UnityEngine;
using SheetSync;
using GlobalCCSettings = SheetSync.GlobalCCSettings;

namespace SheetSync
{
    /// <summary>
    /// SheetSyncWindow のメインビューモデル
    /// 
    /// SheetSync のメインウィンドウのすべてのビジネスロジックと状態管理を担当します。
    /// MVVMパターンに従い、View（UI）から独立したテスタブルな設計を実現しています。
    /// 
    /// 主な責任:
    /// - ConvertSetting の一覧管理とフィルタリング
    /// - 各種コマンドの実行と状態管理
    /// - UIに表示するデータの準備と変更通知
    /// - 非同期処理の管理と進捗報告
    /// 
    /// 設計上の特徴:
    /// - UIフレームワークに依存しない（ImGUIでもUIToolkitでも使用可能）
    /// - Repositoryパターンによるデータアクセスの抽象化
    /// - コマンドパターンによるアクションのカプセル化
    /// </summary>
    public class SheetSyncViewModel : ViewModelBase
    {
        private readonly SheetSyncRepository _repository;
        private List<ConvertSettingItemViewModel> _items;
        private string _searchText = "";
        private bool _isProcessing;
        private GlobalCCSettings _globalSettings;
        
        /// <summary>
        /// すべての ConvertSetting アイテムの ViewModel リスト
        /// </summary>
        public List<ConvertSettingItemViewModel> AllItems => _items;
        
        /// <summary>
        /// 検索テキスト。この値が変更されると FilteredItems が自動的に更新されます
        /// </summary>
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    OnPropertyChanged();
                }
            }
        }
        
        public bool IsProcessing
        {
            get => _isProcessing;
            set => SetProperty(ref _isProcessing, value);
        }
        
        /// <summary>
        /// 検索テキストでフィルタリングされたアイテムのコレクション
        /// </summary>
        public IEnumerable<ConvertSettingItemViewModel> FilteredItems
        {
            get
            {
                if (string.IsNullOrEmpty(_searchText))
                    return _items;
                    
                return _items.Where(vm => vm.Model.MatchesSearchText(_searchText));
            }
        }
        
        // Commands
        public ICommand RefreshCommand { get; }
        public ICommand GenerateAllCodeCommand { get; }
        public ICommand CreateAllAssetsCommand { get; }
        
        public SheetSyncViewModel()
        {
            _repository = new SheetSyncRepository();
            _items = new List<ConvertSettingItemViewModel>();
            
            // Initialize commands
            RefreshCommand = new RelayCommand(Refresh);
            GenerateAllCodeCommand = new RelayCommand(GenerateAllCode, CanExecuteBatchCommand);
            CreateAllAssetsCommand = new RelayCommand(CreateAllAssets, CanExecuteBatchCommand);
            
            // Initial load
            Refresh();
        }
        
        /// <summary>
        /// ConvertSetting の一覧を再読み込みし、UIを更新します
        /// </summary>
        public void Refresh()
        {
            _repository.RefreshCache();
            _globalSettings = _repository.GetGlobalSettings();
            
            _items.Clear();
            foreach (var model in _repository.GetAllSettings())
            {
                var viewModel = new ConvertSettingItemViewModel(model);
                _items.Add(viewModel);
            }
            
            OnPropertyChanged();
        }
        
        private bool CanExecuteBatchCommand()
        {
            return !_isProcessing && _items.Any();
        }
        
        private void GenerateAllCode()
        {
            if (_isProcessing) return;
            
            IsProcessing = true;
            
            try
            {
                var settings = _items.Select(vm => vm.Model.Settings).ToArray();
                SheetSyncService.GenerateAllCode(settings, _globalSettings);
            }
            finally
            {
                IsProcessing = false;
            }
        }
        
        private void CreateAllAssets()
        {
            if (_isProcessing) return;
            
            IsProcessing = true;
            
            try
            {
                var settings = _items.Select(vm => vm.Model.Settings).ToArray();
                SheetSyncService.CreateAllAssets(settings, _globalSettings);
            }
            finally
            {
                IsProcessing = false;
            }
        }
        
        /// <summary>
        /// 指定された ConvertSetting のインポート処理を実行します
        /// </summary>
        /// <param name="itemViewModel">インポート対象の ViewModel</param>
        public void ExecuteImport(ConvertSettingItemViewModel itemViewModel)
        {
            if (_isProcessing) return;
            
            itemViewModel.UpdateStatus("Importing...", true);
            KoheiUtils.EditorCoroutineRunner.StartCoroutine(ExecuteImportCoroutine(itemViewModel));
        }
        
        public void ExecuteDownload(ConvertSettingItemViewModel itemViewModel)
        {
            if (_isProcessing) return;
            
            itemViewModel.UpdateStatus("Downloading...", true);
            KoheiUtils.EditorCoroutineRunner.StartCoroutine(SheetSyncService.ExecuteDownload(itemViewModel.Model.Settings));
        }
        
        public void GenerateCode(ConvertSettingItemViewModel itemViewModel)
        {
            if (_isProcessing) return;
            
            IsProcessing = true;
            itemViewModel.UpdateStatus("Generating code...", true);
            
            try
            {
                SheetSyncService.GenerateOneCode(itemViewModel.Model.Settings, _globalSettings);
                itemViewModel.UpdateStatus("Code generated successfully");
            }
            catch (Exception ex)
            {
                itemViewModel.UpdateStatus($"Error: {ex.Message}");
                Debug.LogException(ex);
            }
            finally
            {
                IsProcessing = false;
                itemViewModel.IsProcessing = false;
            }
        }
        
        public void CreateAssets(ConvertSettingItemViewModel itemViewModel)
        {
            if (_isProcessing) return;
            
            itemViewModel.UpdateStatus("Creating assets...", true);
            
            try
            {
                var job = new SheetSync.CreateAssetsJob(itemViewModel.Model.Settings);
                job.Execute();
                itemViewModel.UpdateStatus("Assets created successfully");
                
                // Update output reference
                itemViewModel.Model.UpdateOutputReference();
            }
            catch (Exception ex)
            {
                itemViewModel.UpdateStatus($"Error: {ex.Message}");
                Debug.LogException(ex);
            }
            finally
            {
                itemViewModel.IsProcessing = false;
            }
        }
        
        private IEnumerator ExecuteImportCoroutine(ConvertSettingItemViewModel itemViewModel)
        {
            yield return SheetSyncService.ExecuteImport(itemViewModel.Model.Settings);
            
            itemViewModel.UpdateStatus("Import completed");
            itemViewModel.IsProcessing = false;
            
            // Update output reference after import
            itemViewModel.Model.UpdateOutputReference();
        }
    }
}
