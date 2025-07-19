# SheetSync 使い方ガイド

## 概要
SheetSyncは、Google SpreadsheetsとUnityのScriptableObjectを同期するツールです。
読み取りと書き込みの両方に対応し、3つの認証方式をサポートしています。

## 目次
1. [初期セットアップ](#初期セットアップ)
2. [スプレッドシートからの読み込み（Import）](#スプレッドシートからの読み込みimport)
3. [スプレッドシートへの書き込み（Update）](#スプレッドシートへの書き込みupdate)
4. [認証方式の選択](#認証方式の選択)
5. [トラブルシューティング](#トラブルシューティング)

## 初期セットアップ

### 1. Google Cloud Consoleの設定
1. [Google Cloud Console](https://console.cloud.google.com/)にアクセス
2. 新規プロジェクトを作成（または既存のものを選択）
3. 「APIとサービス」→「ライブラリ」から「Google Sheets API」を有効化

### 2. 認証方式の選択
以下の2つから選択：
- **APIキー**: 読み取り専用（最も簡単）
- **サービスアカウント**: 読み書き可能（自動化に最適）

## スプレッドシートからの読み込み（Import）

### 1. ConvertSettingの作成
```
1. Projectウィンドウで右クリック
2. Create > SheetSync > ConverterSettings
3. 作成されたConvertSettingアセットを選択
```

### 2. ConvertSettingの設定
```csharp
// 基本設定
Sheet ID: [Google SpreadsheetsのID]
Gid: [シートのGID（URLから取得）]
Class Name: CharacterData（生成するクラス名）
Key: humanId（主キーとなる列名）

// パスの設定
Destination: Assets/Data/Characters/（ScriptableObjectの保存先）
Code Destination: Assets/Scripts/Generated/（生成コードの保存先）

// オプション
Class Generate: true（クラスを自動生成する場合）
Table Generate: false（テーブルクラスは不要な場合）
```

### 3. スプレッドシートの形式
```
| humanId | name    | level | hp  | attack |
|---------|---------|-------|-----|--------|
| int     | string  | int   | int | int    |
| 1       | Taro    | 10    | 100 | 20     |
| 2       | Hanako  | 15    | 150 | 30     |
```
- 1行目: フィールド名
- 2行目: 型情報
- 3行目以降: データ

### 4. インポートの実行
```
1. Tools > SheetSync > CSV Converter を開く
2. ConvertSettingを選択
3. 「インポート実行」をクリック
```

## スプレッドシートへの書き込み（Update）

### 方法1: APIキー（読み取り専用）
```
注意: APIキーでは書き込みができません。
読み取り専用のアクセスに使用してください。
```

### 方法2: サービスアカウント認証

#### セットアップ
1. Google Cloud Consoleでサービスアカウントを作成
2. JSONキーをダウンロード
3. `ProjectSettings/SheetSync/service-account-key.json`に配置
4. **重要**: スプレッドシートをサービスアカウントのメールアドレスと共有

#### 使用方法
```
1. Tools > SheetSync > Update Records (Service Account) を開く
2. 「認証を開始」をクリック
3. 更新したいデータを入力：
   - キー列名: humanId
   - 検索値: 1
   - 更新する列名: name
   - 新しい値: Tanaka
4. 「更新を実行」をクリック
```

## 認証方式の選択

| 認証方式 | 読み取り | 書き込み | ユーザー操作 | 自動化 | 用途 |
|---------|---------|---------|--------------|--------|------|
| APIキー | ✅ | ❌ | 不要 | ✅ | 読み取り専用、開発環境 |
| サービスアカウント | ✅ | ✅ | 不要 | ✅ | CI/CD、自動化、本番環境 |

## 実践例

### 例1: キャラクターデータの管理
```csharp
// 1. スプレッドシートからキャラクターデータをインポート
// 2. ゲーム内でキャラクターのレベルアップ
// 3. レベルアップ結果をスプレッドシートに反映

// ConvertSettingで生成されたCharacterDataクラス
[System.Serializable]
public class CharacterData : ScriptableObject
{
    public int humanId;
    public string name;
    public int level;
    public int hp;
    public int attack;
}

// 更新例（サービスアカウント使用）
var updateData = new Dictionary<string, object>
{
    { "level", 11 },
    { "hp", 110 }
};

await updateService.UpdateRowAsync(
    spreadsheetId: "your-spreadsheet-id",
    sheetName: "Characters",
    keyColumn: "humanId",
    keyValue: "1",
    updateData: updateData
);
```

### 例2: バッチ更新
```csharp
// 複数のキャラクターを一括更新
var updates = new Dictionary<string, Dictionary<string, object>>
{
    ["1"] = new Dictionary<string, object> { { "level", 11 } },
    ["2"] = new Dictionary<string, object> { { "level", 16 } },
    ["3"] = new Dictionary<string, object> { { "level", 21 } }
};

await updateService.UpdateMultipleRowsAsync(
    spreadsheetId: "your-spreadsheet-id",
    sheetName: "Characters",
    keyColumn: "humanId",
    updates: updates
);
```

## トラブルシューティング

### 「APIキーで書き込みができない」
- APIキーは読み取り専用です
- OAuth2またはサービスアカウント認証を使用してください

### 「Permission denied」エラー（サービスアカウント）
- スプレッドシートの共有設定を確認
- サービスアカウントのメールアドレス（client_email）を「編集者」として追加

### 「service-account-key.jsonが見つかりません」
- 正しいパスに配置: `ProjectSettings/SheetSync/`
- サービスアカウントキーがJSON形式であることを確認

## ベストプラクティス

1. **開発環境**: APIキー（読み取り専用）またはサービスアカウント
2. **本番環境**: サービスアカウント認証（自動化、CI/CD対応）
3. **セキュリティ**: 認証ファイルは必ず.gitignoreに追加
4. **バックアップ**: 重要なデータは定期的にバックアップ
5. **権限管理**: 最小限の権限のみ付与（読み取り専用が可能なら書き込み権限は付与しない）

## 関連ドキュメント
- [ServiceAccount_Setup_Guide.md](ServiceAccount_Setup_Guide.md) - サービスアカウントの詳細設定
- [SPEC_Update_Feature.md](SPEC_Update_Feature.md) - 更新機能の技術仕様