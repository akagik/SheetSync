using System;

namespace SheetSync
{
    /// <summary>
    /// プロパティ変更通知インターフェース
    /// 
    /// ViewModelのプロパティが変更されたときに、Viewに通知するための仕組みを提供します。
    /// このインターフェースにより、データバインディングとMVVMパターンの実装が可能になります。
    /// 
    /// 使用例:
    /// - ViewModelでプロパティが変更されたときに PropertyChanged イベントを発火
    /// - View側でイベントを購読し、UIを自動的に更新
    /// - UIToolkit への移行時にもこの仕組みを活用可能
    /// </summary>
    public interface INotifyPropertyChanged
    {
        event Action PropertyChanged;
    }
    
    /// <summary>
    /// ViewModelの基底クラス
    /// 
    /// すべてのViewModelが継承すべき基底クラスです。
    /// プロパティ変更通知の基本的な実装を提供し、派生クラスでの実装を簡素化します。
    /// 
    /// 主な機能:
    /// - INotifyPropertyChanged の実装
    /// - SetProperty メソッドによる簡潔なプロパティセッターの記述
    /// - OnPropertyChanged による手動での変更通知
    /// 
    /// 使用方法:
    /// <code>
    /// public class MyViewModel : ViewModelBase
    /// {
    ///     private string _name;
    ///     public string Name
    ///     {
    ///         get => _name;
    ///         set => SetProperty(ref _name, value);
    ///     }
    /// }
    /// </code>
    /// </summary>
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        public event Action PropertyChanged;
        
        protected virtual void OnPropertyChanged()
        {
            PropertyChanged?.Invoke();
        }
        
        /// <summary>
        /// プロパティの値を設定し、変更があった場合は通知を発行します
        /// </summary>
        /// <typeparam name="T">プロパティの型</typeparam>
        /// <param name="field">バッキングフィールドへの参照</param>
        /// <param name="value">新しい値</param>
        /// <returns>値が変更された場合は true、変更されなかった場合は false</returns>
        protected bool SetProperty<T>(ref T field, T value)
        {
            if (!Equals(field, value))
            {
                field = value;
                OnPropertyChanged();
                return true;
            }
            return false;
        }
    }
}
