# SheetSyncWindow リファクタリング計画

## 現状の問題点

1. **UIとロジックの混在**
   - OnGUI内にビジネスロジックが直接記述されている
   - データ取得、フィルタリング、実行処理がUI層に混在
   
2. **責任の不明確さ**
   - 設定の管理、UI表示、コマンド実行が1つのクラスに集約
   - 静的メソッドが多く、テスタビリティが低い

3. **UIToolkit移行の困難さ**
   - ImGUIに強く依存した実装
   - データバインディングの仕組みがない

## 新しいアーキテクチャ

### 1. MVVM パターンの採用

```
View (UI層)
  ↓↑
ViewModel (プレゼンテーション層)
  ↓↑
Model (ビジネスロジック層)
```

### 2. クラス設計

#### Models/
- **ConvertSettingItem** - 個別の設定を表すモデル
- **SheetSyncRepository** - 設定の取得・管理

#### ViewModels/
- **SheetSyncViewModel** - メインのビューモデル
- **ConvertSettingItemViewModel** - 個別設定のビューモデル
- **ICommand** - コマンドパターンの実装

#### Views/
- **SheetSyncWindow** - UI表示のみ（ImGUI版）
- **SheetSyncUIToolkitWindow** - 将来のUIToolkit版

#### Commands/
- **ImportCommand** - インポート処理
- **GenerateCodeCommand** - コード生成処理
- **CreateAssetsCommand** - アセット作成処理
- **OpenSpreadsheetCommand** - スプレッドシートを開く

### 3. 主な変更点

1. **データバインディング**
   - ViewModelがINotifyPropertyChangedを実装
   - UIは変更通知を受けて自動更新

2. **コマンドパターン**
   - ボタンアクションをコマンドとして実装
   - 実行可能条件の管理

3. **依存性の注入**
   - RepositoryやServiceをViewModelに注入
   - テスタブルな設計

4. **非同期処理**
   - async/awaitの活用
   - UIブロッキングの回避

## 実装ステップ

1. Model層の実装
2. ViewModel層の実装
3. Command層の実装
4. 既存UIの置き換え
5. テストの追加