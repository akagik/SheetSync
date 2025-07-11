using System;
using System.Threading.Tasks;

namespace SheetSync
{
    /// <summary>
    /// コマンドインターフェース
    /// 
    /// コマンドパターンを実装するための基本インターフェースです。
    /// UIイベント（ボタンクリック、メニュー選択など）をビジネスロジックから分離し、
    /// テスタブルで再利用可能なコードを実現します。
    /// 
    /// 主な用途:
    /// - ボタンクリック時のアクションの定義
    /// - メニュー項目の実行ロジック
    /// - キーボードショートカットの処理
    /// </summary>
    public interface ICommand
    {
        bool CanExecute();
        void Execute();
    }
    
    /// <summary>
    /// 非同期コマンドインターフェース
    /// 
    /// 非同期処理を実行するコマンドのためのインターフェースです。
    /// ファイルI/O、ネットワーク通信、長時間の計算など、
    /// UIをブロックしないで実行したい処理に使用します。
    /// 
    /// 特徴:
    /// - async/await パターンのサポート
    /// - キャンセル可能な操作の実装が可能
    /// - 進捗報告機能との統合が容易
    /// </summary>
    public interface IAsyncCommand
    {
        bool CanExecute();
        Task ExecuteAsync();
    }
    
    /// <summary>
    /// パラメータ付きコマンドインターフェース
    /// 
    /// 実行時にパラメータを受け取るコマンドのためのインターフェースです。
    /// 選択されたアイテムに対する操作や、入力値に基づく処理などに使用します。
    /// 
    /// 使用例:
    /// - リストアイテムの選択時のアクション
    /// - テキスト入力に応じた検索処理
    /// - コンテキストメニューの対象アイテムへの操作
    /// </summary>
    /// <typeparam name="T">コマンドパラメータの型</typeparam>
    public interface ICommand<T>
    {
        bool CanExecute(T parameter);
        void Execute(T parameter);
    }
    
    /// <summary>
    /// 基本的なコマンド実装
    /// 
    /// ICommand インターフェースの汎用的な実装です。
    /// デリゲートを使用して実行ロジックを注入できるため、
    /// 簡単にコマンドを作成できます。
    /// 
    /// 使用例:
    /// <code>
    /// var saveCommand = new RelayCommand(
    ///     execute: () => SaveData(),
    ///     canExecute: () => HasChanges
    /// );
    /// </code>
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;
        
        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }
        
        public bool CanExecute() => _canExecute?.Invoke() ?? true;
        public void Execute() => _execute();
    }
    
    /// <summary>
    /// パラメータ付きコマンド実装
    /// </summary>
    public class RelayCommand<T> : ICommand<T>
    {
        private readonly Action<T> _execute;
        private readonly Func<T, bool> _canExecute;
        
        public RelayCommand(Action<T> execute, Func<T, bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }
        
        public bool CanExecute(T parameter) => _canExecute?.Invoke(parameter) ?? true;
        public void Execute(T parameter) => _execute(parameter);
    }
}
