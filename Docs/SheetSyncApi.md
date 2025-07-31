# SheetSync API ドキュメント

## 概要

SheetSync API は、Unity外部からGoogle Sheetsの操作を行うための静的APIを提供します。MCP (Model Context Protocol) サーバー経由でAIやその他の外部システムから呼び出すことを想定して設計されています。

## アーキテクチャ

```
AI/外部システム → MCP → Unity Static Method (SheetSyncApi) → SheetSync Services
```

## 主な特徴

- **静的メソッド**: すべてのAPIは静的メソッドとして実装
- **JSON通信**: 複雑なデータ構造はJSON文字列として受け渡し
- **同期的実行**: 非同期処理を内部で同期的に実行して結果を返す
- **統一レスポンス**: ApiResponse構造による一貫したエラーハンドリング
- **タイムアウト対応**: 長時間処理に対するタイムアウト機能

## 利用方法

### MCP経由での呼び出し例

```json
// invoke_static_method を使用
{
  "typeName": "SheetSync.Api.SheetSyncApi",
  "methodName": "UpdateRow",
  "parameters": [
    {
      "type": "string",
      "value": "{\"spreadsheetId\":\"1234567890\",\"sheetName\":\"Sheet1\",\"keyColumn\":\"ID\",\"keyValue\":\"123\",\"updateData\":{\"Name\":\"Updated Name\",\"Age\":\"30\"}}"
    }
  ]
}
```

## API リファレンス

### 認証関連

#### InitializeAuth
サービスアカウント認証を初期化します。

```csharp
public static string InitializeAuth(string credentialsPath)
```

**パラメータ:**
- `credentialsPath`: Google サービスアカウントの認証情報JSONファイルのパス

**レスポンス例:**
```json
{
  "success": true,
  "data": "Authentication successful"
}
```

#### CheckAuthStatus
現在の認証状態を確認します。

```csharp
public static string CheckAuthStatus()
```

**レスポンス例:**
```json
{
  "success": true,
  "data": {
    "isAuthenticated": true,
    "hasValidCredentials": true
  }
}
```

### データ更新

#### UpdateRow
スプレッドシートの単一行を更新します。

```csharp
public static string UpdateRow(string requestJson)
```

**リクエスト構造:**
```json
{
  "spreadsheetId": "your-spreadsheet-id",
  "sheetName": "Sheet1",
  "keyColumn": "ID",
  "keyValue": "123",
  "updateData": {
    "Name": "Updated Name",
    "Age": "30",
    "Status": "Active"
  }
}
```

**レスポンス例:**
```json
{
  "success": true,
  "data": true
}
```

#### UpdateMultipleRows
複数行を一括で更新します。

```csharp
public static string UpdateMultipleRows(string requestJson)
```

**リクエスト構造:**
```json
{
  "spreadsheetId": "your-spreadsheet-id",
  "sheetName": "Sheet1",
  "keyColumn": "ID",
  "updates": {
    "123": {
      "Name": "User 123",
      "Status": "Active"
    },
    "456": {
      "Name": "User 456",
      "Status": "Inactive"
    }
  }
}
```

**レスポンス例:**
```json
{
  "success": true,
  "data": {
    "success": true,
    "rowCount": 2
  }
}
```

### ユーティリティ

#### GetApiInfo
利用可能なAPIメソッドの情報を取得します。

```csharp
public static string GetApiInfo()
```

#### GetSampleUpdateRequest
UpdateRowメソッド用のサンプルリクエストを取得します。

```csharp
public static string GetSampleUpdateRequest()
```

#### GetSampleBatchUpdateRequest
UpdateMultipleRowsメソッド用のサンプルリクエストを取得します。

```csharp
public static string GetSampleBatchUpdateRequest()
```

## エラーハンドリング

すべてのAPIメソッドは統一されたエラーレスポンスを返します：

```json
{
  "success": false,
  "error": "エラーメッセージ",
  "details": "詳細なエラー情報（オプション）"
}
```

### 一般的なエラー

- **認証エラー**: サービスアカウント認証が初期化されていない
- **パラメータエラー**: 必須パラメータが不足している
- **タイムアウト**: 処理が指定時間内に完了しなかった
- **ネットワークエラー**: Google Sheets APIへの接続に失敗

## 使用例

### 1. 認証の初期化

```csharp
// MCP経由での呼び出し
var authResult = SheetSyncApi.InitializeAuth("/path/to/credentials.json");
```

### 2. 単一行の更新

```csharp
// リクエストの準備
var request = new {
    spreadsheetId = "1234567890",
    sheetName = "UserData",
    keyColumn = "UserID",
    keyValue = "U001",
    updateData = new Dictionary<string, object> {
        ["LastLogin"] = DateTime.Now.ToString(),
        ["Status"] = "Online"
    }
};

// JSON化して送信
var requestJson = JsonConvert.SerializeObject(request);
var result = SheetSyncApi.UpdateRow(requestJson);
```

### 3. 複数行の一括更新

```csharp
// バッチ更新リクエスト
var batchRequest = new {
    spreadsheetId = "1234567890",
    sheetName = "Inventory",
    keyColumn = "ItemID",
    updates = new Dictionary<string, Dictionary<string, object>> {
        ["ITEM001"] = new Dictionary<string, object> {
            ["Stock"] = "50",
            ["LastUpdated"] = DateTime.Now.ToString()
        },
        ["ITEM002"] = new Dictionary<string, object> {
            ["Stock"] = "0",
            ["Status"] = "Out of Stock"
        }
    }
};

var requestJson = JsonConvert.SerializeObject(batchRequest);
var result = SheetSyncApi.UpdateMultipleRows(requestJson);
```

## 今後の拡張

以下の機能は今後実装予定です：

- **InsertRow / InsertMultipleRows**: 新規行の挿入
- **DeleteRow / DeleteMultipleRows**: 行の削除
- **GetSheetData**: シートデータの読み込み
- **CreateSheet / DeleteSheet**: シートの作成・削除
- **実行履歴の記録**: API呼び出しのログ機能

## 注意事項

1. **認証**: APIを使用する前に必ずInitializeAuthで認証を初期化してください
2. **タイムアウト**: 単一行更新は30秒、複数行更新は60秒でタイムアウトします
3. **同時実行**: 複数のAPI呼び出しを同時に実行することは推奨されません
4. **データ型**: updateDataの値はすべて文字列として送信されます

## トラブルシューティング

### 認証に失敗する場合
- 認証情報ファイルのパスが正しいか確認
- ファイルの読み取り権限があるか確認
- Google Cloud Consoleでサービスアカウントが有効か確認

### 更新が反映されない場合
- キー列の値が正確に一致しているか確認
- シート名が正しいか確認
- 列名の大文字小文字が一致しているか確認

### タイムアウトが発生する場合
- ネットワーク接続を確認
- 更新対象の行数を減らして再試行
- Google Sheets APIの制限に達していないか確認