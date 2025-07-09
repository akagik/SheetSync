# フォルダ構成整理計画

## 現在の構成の問題点
- ルートディレクトリにファイルが散在している
- 機能別の整理が不十分
- 関連するファイルが離れている

## 新しいフォルダ構成案

```
SheetSync/
├── Core/               # コア機能（データ処理、変換）
│   ├── Converters/     # 型変換関連
│   ├── Generators/     # コード生成関連
│   ├── Importers/      # インポート処理
│   └── Validators/     # バリデーション
├── Data/               # データモデル
│   ├── Models/         # ScriptableObjectモデル
│   └── Runtime/        # ランタイムで使用するデータ構造
├── Editor/             # エディタ拡張
│   ├── Windows/        # EditorWindow
│   ├── Inspectors/     # カスタムインスペクタ
│   └── MenuItems/      # メニューアイテム
├── Infrastructure/     # 基盤・ユーティリティ
│   ├── FileSystem/     # ファイル操作
│   ├── Reflection/     # リフレクション関連
│   └── Csv/           # CSV処理
├── Migration/          # 移行ツール（既存）
├── Templates/          # テンプレート（既存）
└── Old/               # 古いコード（既存）
```

## ファイル移動計画

### Core/Converters/
- Str2TypeConverter.cs → Core/Converters/TypeConverter.cs

### Core/Generators/
- ClassGenerator.cs → Core/Generators/ClassCodeGenerator.cs
- EnumGenerator.cs → Core/Generators/EnumCodeGenerator.cs
- AssetsGenerator.cs → Core/Generators/AssetGenerator.cs

### Core/Importers/
- CsvConvert.cs → Core/Importers/CsvImporter.cs
- CreateAssetsJob.cs → Core/Importers/AssetCreationJob.cs

### Data/Models/
- ConvertSetting.cs （既存）
- GlobalCCSettings.cs （既存）
- Field.cs → Data/Models/FieldDefinition.cs

### Data/Runtime/
- CsvData.cs → Data/Runtime/CsvData.cs

### Editor/Windows/
- SheetSyncWindow.cs → Editor/Windows/SheetSyncWindow.cs
- CCSettingsEditWindow.cs → Editor/Windows/SettingsEditWindow.cs

### Infrastructure/
- Logic/CCLogic.cs → Infrastructure/Utilities/PathUtility.cs
- Logic/CsvLogic.cs → Infrastructure/Csv/CsvProcessor.cs
- CsvReflectionCache.cs → Infrastructure/Reflection/ReflectionCache.cs

### 削除対象
- ConvertSetting.cs（ルート） - コメントアウトされているため
- GlobalCCSettings.cs（ルート） - コメントアウトされているため