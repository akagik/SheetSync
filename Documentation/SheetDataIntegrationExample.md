# SheetDataIntegrationExample クラスガイド

## 概要

`SheetDataIntegrationExample` は、Google Sheets との双方向データ同期を実現するための実装パターンを示すヘルパークラスです。このクラスは、`ExtendedSheetData` の変更追跡機能を活用し、既存の SheetSync サービスと連携して効率的なデータ同期を実現します。

## 主な機能

### 1. データの読み込み
- Google Sheets からデータを読み込み、`ExtendedSheetData` インスタンスを作成
- ヘッダー行の自動検出またはGlobalCCSettings からの取得
- ヘッダー行に基づいたデータビューの作成

### 2. 変更追跡と差分管理
- データの挿入、更新、削除の自動追跡
- 変更履歴の管理と差分表示
- キーベースの高速検索とデータ操作

### 3. 自動同期処理
- 変更の種類（挿入/更新/削除）に応じた自動振り分け
- 既存のサービス（SheetUpdateServiceAccountService、SheetInsertServiceAccountService）との連携
- エラーハンドリングとロギング

## 使用方法

### 基本的な使用パターン

```csharp
// 1. データの読み込み
var sheetData = await SheetDataIntegrationExample.LoadSheetDataAsync(
    spreadsheetId: "your-spreadsheet-id",
    sheetName: "Sheet1"
);

// 2. キーインデックスの構築（高速検索用）
sheetData.BuildKeyIndex("ID");

// 3. データの編集
// 既存行の更新
sheetData.UpdateRowByKey("ID", "123", new Dictionary<string, object>
{
    ["Name"] = "更新された名前",
    ["Age"] = "30"
});

// 新しい行の挿入
sheetData.InsertRow(5, new Dictionary<string, object>
{
    ["ID"] = "999",
    ["Name"] = "新規ユーザー",
    ["Age"] = "25"
});

// 4. 変更の確認
foreach (var change in sheetData.Changes)
{
    Debug.Log($"変更: {change.Type} - 行{change.RowIndex}");
}

// 5. 変更をスプレッドシートに適用
bool success = await SheetDataIntegrationExample.UpdateSpreadsheetWithSheetData(
    spreadsheetId,
    sheetName,
    sheetData
);
```

### 完全な実装例

```csharp
// ExampleEditAndShowDiff メソッドの使用
bool success = await SheetDataIntegrationExample.ExampleEditAndShowDiff(
    spreadsheetId: "your-spreadsheet-id",
    sheetName: "Sheet1"
);
```

## メソッド詳細

### LoadSheetDataAsync
スプレッドシートからデータを読み込み、ExtendedSheetData インスタンスを作成します。

**パラメータ:**
- `spreadsheetId`: Google Sheets のスプレッドシートID
- `sheetName`: 読み込み対象のシート名
- `verbose`: 詳細ログの出力有無（デフォルト: true）

**戻り値:**
- 成功時: ExtendedSheetData インスタンス
- 失敗時: null

**特徴:**
- GlobalCCSettings の rowIndexOfName 設定を優先的に使用
- 設定が無効な場合は自動的にヘッダー行を検出
- ヘッダー行から始まるデータビューを自動作成

### UpdateSpreadsheetWithSheetData
ExtendedSheetData に記録された変更をスプレッドシートに適用します。

**パラメータ:**
- `spreadsheetId`: 更新対象のスプレッドシートID
- `sheetName`: 更新対象のシート名
- `sheetData`: 変更が記録された ExtendedSheetData インスタンス
- `verbose`: 詳細ログの出力有無（デフォルト: true）

**戻り値:**
- 成功時: true
- 失敗時: false

**処理順序:**
1. 削除処理（降順でインデックスのずれを防ぐ）
2. 挿入処理（新規行の追加）
3. 更新処理（既存行の変更）

### ExampleEditAndShowDiff
データの編集と差分表示の完全な実装例を提供します。

**実行内容:**
1. データの読み込み
2. キーインデックスの構築
3. サンプルデータの編集（更新と挿入）
4. 変更差分の表示
5. 変更の適用

## 注意事項

### 認証について
- Google Service Account 認証が必要です
- `GoogleServiceAccountAuth.IsAuthenticated` で認証状態を確認

### キーカラムについて
- 現在の実装では "ID" カラムを主キーとして使用
- 実際の使用時は、プロジェクトに応じて適切なキーカラムを指定してください

### 削除機能について
- 現在、行削除機能は未実装です（TODO コメント参照）
- 削除が必要な場合は、独自の実装が必要です

### エラーハンドリング
- 各メソッドは try-catch でエラーをキャッチし、適切にログ出力します
- verbose パラメータでログ出力レベルを制御できます

## 拡張方法

### カスタムキーカラムの使用
`GetRowKey` メソッドを修正して、異なるキーカラムを使用できます：

```csharp
private static string GetRowKey(ExtendedSheetData sheetData, int rowIndex)
{
    // "ID" の代わりに任意のカラム名を使用
    var keyColumnIndex = sheetData.GetColumnIndex("YourKeyColumn");
    // ... 以下同じ
}
```

### 削除機能の実装
TODO コメントがある箇所に、削除サービスを実装できます：

```csharp
if (deletions.Count > 0)
{
    var deleteService = new SheetDeleteServiceAccountService(); // 要実装
    success &= await deleteService.DeleteRowsAsync(
        spreadsheetId, sheetName, deletions, verbose);
}
```

### バッチ処理の最適化
大量のデータ変更がある場合、バッチ処理を実装することで性能を向上できます。

## 関連クラス

- `ExtendedSheetData`: 変更追跡機能を持つシートデータクラス
- `SheetUpdateServiceAccountService`: 行更新サービス
- `SheetInsertServiceAccountService`: 行挿入サービス
- `HeaderDetector`: ヘッダー行検出ユーティリティ
- `GoogleServiceAccountAuth`: Google認証サービス

## まとめ

`SheetDataIntegrationExample` は、SheetSync の各種サービスを統合して使用するための実装パターンを提供します。このクラスを参考にして、プロジェクト固有の要件に合わせたカスタマイズを行うことができます。