# SheetSync クイックスタートガイド

## 5分で始めるSheetSync

### ステップ1: Google Sheetsの準備
1. [サンプルスプレッドシート](https://docs.google.com/spreadsheets/d/1BxiMVs0XRA5nFMdKvBdBZjgmUUqptlbs74OgvE2upms/edit)をコピー
2. 自分のGoogleドライブに保存
3. URLから以下を確認：
   - Sheet ID: `1BxiMVs0XRA5nFMdKvBdBZjgmUUqptlbs74OgvE2upms`（例）
   - GID: URLの`#gid=0`の部分

### ステップ2: 認証の選択

#### 🟢 読み取りのみ（最も簡単）
```
1. Tools > SheetSync > CSV Converter
2. 初回起動時にAPIキーを入力
3. Google Cloud ConsoleでAPIキーを作成：
   - https://console.cloud.google.com/apis/credentials
   - 「認証情報を作成」→「APIキー」
```

#### 🔵 読み書き両方（OAuth2）
```
1. Google Cloud ConsoleでOAuth2クライアントを作成
   - アプリケーションの種類: デスクトップアプリ
2. credentials.jsonをダウンロード
3. ProjectSettings/SheetSync/credentials.json に配置
4. Tools > SheetSync > Update Records (OAuth2)
```

#### 🟣 自動化向け（サービスアカウント）
```
1. Google Cloud Consoleでサービスアカウントを作成
2. JSONキーをダウンロード
3. ProjectSettings/SheetSync/service-account-key.json に配置
4. スプレッドシートをサービスアカウントと共有（重要！）
5. Tools > SheetSync > Update Records (Service Account)
```

### ステップ3: データのインポート

1. **ConvertSettingを作成**
   ```
   Project右クリック → Create → SheetSync → ConverterSettings
   ```

2. **設定を入力**
   ```
   Sheet ID: [コピーしたスプレッドシートのID]
   Class Name: CharacterData
   Destination: Assets/Data/
   Code Destination: Assets/Scripts/Generated/
   ```

3. **インポート実行**
   ```
   Tools > SheetSync > CSV Converter
   ConvertSettingを選択 → インポート実行
   ```

### ステップ4: データの更新（書き込み）

#### OAuth2を使用した更新
```csharp
// 1. ウィンドウを開く
Tools > SheetSync > Update Records (OAuth2)

// 2. 認証
「認証を開始」ボタンをクリック

// 3. 更新
キー列名: humanId
検索値: 1
更新する列名: name
新しい値: Tanaka

「更新を実行」をクリック
```

## サンプルスプレッドシート形式

| humanId | name   | level | hp  | attack | description |
|---------|--------|-------|-----|--------|-------------|
| int     | string | int   | int | int    | string      |
| 1       | Taro   | 10    | 100 | 20     | 主人公      |
| 2       | Hanako | 15    | 150 | 30     | ヒロイン    |

## よくある質問

### Q: APIキーはどこで取得しますか？
A: [Google Cloud Console](https://console.cloud.google.com/)で作成します。Sheets APIを有効化することを忘れずに。

### Q: 「Permission denied」エラーが出ます
A: 
- APIキー: スプレッドシートが「リンクを知っている全員」に公開されているか確認
- サービスアカウント: スプレッドシートがサービスアカウントのメールと共有されているか確認

### Q: 書き込みができません
A: APIキーは読み取り専用です。書き込みにはOAuth2またはサービスアカウント認証を使用してください。

## 次のステップ
- [詳細な使い方ガイド](USAGE_GUIDE.md)
- [OAuth2セットアップ](OAuth2_Setup_Guide.md)
- [サービスアカウントセットアップ](ServiceAccount_Setup_Guide.md)