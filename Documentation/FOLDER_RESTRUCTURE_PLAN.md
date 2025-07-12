# SheetSync フォルダ構造整理計画書

## 概要

このドキュメントは、Editor/SheetSync ディレクトリの現在の構造を分析し、より整理された保守性の高い構造への移行計画を提案します。

## 現状の問題点

### 1. 重複したフォルダ構造
- `Data/Models/` と `Models/` の2つのModelsフォルダが存在
- `Utils/` と `Infrastructure/Utilities/` の役割が重複

### 2. 機能の分散
- CSV関連機能が Core, Data, Infrastructure に分散
- 変換処理が複数の場所に存在

### 3. 命名の不統一
- CCLogic, CCSettings などレガシーな命名が残存
- 新旧の命名規則が混在

### 4. 階層の深さと不均衡
- 一部のフォルダは深すぎる階層（例：Data/Runtime に1ファイルのみ）
- 他のフォルダは浅すぎて多数のファイルが混在

## 提案する新構造

```
Editor/SheetSync/
├── Core/                    # コア機能（ビジネスロジック）
│   ├── Conversion/         # 変換処理の中核
│   │   ├── CsvConverter.cs
│   │   ├── TypeConverter.cs
│   │   └── ConversionContext.cs
│   ├── Generation/         # コード生成
│   │   ├── ClassGenerator.cs
│   │   ├── EnumGenerator.cs
│   │   ├── AssetsGenerator.cs
│   │   └── Templates/      # テンプレートファイル
│   ├── Import/            # インポート処理
│   │   ├── ImportService.cs
│   │   ├── ImportJob.cs
│   │   └── ImportContext.cs
│   └── Export/            # エクスポート処理（将来用）
│
├── Data/                   # データ層
│   ├── Interfaces/        # インターフェース定義
│   │   ├── ICsvData.cs
│   │   ├── ICsvDataProvider.cs
│   │   └── ISheetRepository.cs
│   ├── Models/            # すべてのモデル（統合）
│   │   ├── ConvertSetting.cs
│   │   ├── ConvertSettingItem.cs
│   │   ├── Field.cs
│   │   ├── GlobalSettings.cs
│   │   ├── ResultType.cs
│   │   └── SheetDownloadInfo.cs
│   ├── Providers/         # データプロバイダー実装
│   │   ├── FileCsvDataProvider.cs
│   │   ├── GoogleSheetsCsvDataProvider.cs
│   │   └── SheetDataProvider.cs
│   └── Implementations/   # データ実装
│       ├── CsvData.cs
│       ├── SheetData.cs
│       └── SheetRepository.cs
│
├── Infrastructure/         # インフラストラクチャ層
│   ├── Csv/               # CSV処理
│   │   └── CsvParser.cs
│   ├── Reflection/        # リフレクション
│   │   └── ReflectionCache.cs
│   ├── IO/                # ファイルI/O
│   │   └── FileManager.cs
│   └── Logging/           # ロギング
│       └── SheetSyncLogger.cs
│
├── Services/              # アプリケーションサービス
│   ├── SheetSyncService.cs
│   ├── GoogleSheetsService.cs
│   └── ValidationService.cs
│
├── UI/                    # UI関連（EditorWindow, ViewModels）
│   ├── Windows/          # エディターウィンドウ
│   │   ├── SheetSyncWindow.cs
│   │   └── SettingsWindow.cs
│   ├── ViewModels/       # MVVM ViewModels
│   │   ├── MainViewModel.cs
│   │   └── SettingsViewModel.cs
│   ├── Views/            # UIコンポーネント
│   │   └── Components/
│   └── Commands/         # UIコマンド
│       └── ICommand.cs
│
├── Utilities/            # ユーティリティ（統合）
│   ├── EditorCoroutineRunner.cs
│   ├── EditorInputDialog.cs
│   ├── GoogleApiChecker.cs
│   ├── McpConnectionHelper.cs
│   └── PathUtility.cs
│
└── Migration/            # マイグレーション
    └── MigrationUtility.cs
```

### Tests フォルダの移動
```
Packages/SheetSync/
├── Editor/
│   └── SheetSync/
└── Tests/               # Editor と同階層に移動
    └── Editor/
        └── SheetSync.Tests/
```

## 主な変更点

### 1. Models の統合
- `Data/Models/` と `Models/` を `Data/Models/` に統合
- すべてのデータモデルを1箇所に集約

### 2. Core 層の再編成
- Conversion: 変換処理を集約
- Generation: コード生成機能を集約
- Import/Export: インポート/エクスポート処理を分離

### 3. UI 層の独立
- Windows, ViewModels, Commands を UI/ 配下に集約
- UI関連のロジックとビジネスロジックを明確に分離

### 4. Utilities の統合
- Utils/ と Infrastructure/Utilities/ を Utilities/ に統合
- 汎用的なヘルパー機能を1箇所に集約

### 5. 命名の統一
- CCLogic → ConversionUtility
- CCSettings → GlobalSettings
- レガシーな命名をすべて更新

## 移行手順

### Phase 1: 準備（リスクなし）
1. 新フォルダ構造の作成（空フォルダ）
2. 移行スクリプトの作成
3. バックアップの作成

### Phase 2: Models の統合（低リスク）
1. すべてのモデルを Data/Models/ に移動
2. 名前空間の統一
3. 参照の更新

### Phase 3: Core 層の再編成（中リスク）
1. 変換処理を Core/Conversion/ に移動
2. コード生成を Core/Generation/ に移動
3. インポート処理を Core/Import/ に移動

### Phase 4: UI 層の整理（低リスク）
1. Windows を UI/Windows/ に移動
2. ViewModels を UI/ViewModels/ に移動
3. Commands を UI/Commands/ に移動

### Phase 5: Utilities の統合（低リスク）
1. すべてのユーティリティを Utilities/ に統合
2. 重複機能の削除

### Phase 6: クリーンアップ（低リスク）
1. 空フォルダの削除
2. メタファイルの整理
3. ドキュメントの更新

## 期待される効果

1. **開発効率の向上**
   - どこに何があるか明確になる
   - 新機能追加時の配置が明確

2. **保守性の向上**
   - 関連機能が1箇所に集約
   - 責任の分離が明確

3. **テスタビリティの向上**
   - レイヤー間の依存関係が明確
   - モックしやすい構造

4. **オンボーディングの改善**
   - 新規開発者が構造を理解しやすい
   - ドキュメントと実装の一致

## リスクと対策

1. **名前空間の変更**
   - リスク：外部から参照されている可能性
   - 対策：段階的な移行と互換性レイヤーの提供

2. **Git履歴の断絶**
   - リスク：ファイル移動で履歴が見えなくなる
   - 対策：git mv を使用し、移動と編集を分離

3. **ビルドエラー**
   - リスク：参照エラーによるコンパイル失敗
   - 対策：各フェーズ後の完全なテストとコンパイル確認

## タイムライン

- Phase 1-2: 1日
- Phase 3-4: 2日
- Phase 5-6: 1日
- バッファ: 1日

合計: 5営業日

## 次のステップ

1. この計画書のレビューと承認
2. 移行スクリプトの作成
3. Phase 1 の実行