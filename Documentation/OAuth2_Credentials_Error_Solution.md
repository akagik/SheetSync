# OAuth2認証エラー「At least one client secrets」の解決方法

## エラーの内容
```
OAuth2認証エラー: At least one client secrets (Installed or Web) should be set
```

このエラーは、`credentials.json`ファイルが存在しないか、正しい形式でない場合に発生します。

## 解決手順

### 1. Google Cloud Consoleでの設定確認

1. [Google Cloud Console](https://console.cloud.google.com/apis/credentials)にアクセス
2. 適切なプロジェクトが選択されていることを確認
3. 「認証情報」ページで、作成済みのOAuth 2.0クライアントIDを確認

### 2. 正しいOAuth2クライアントの作成

**重要**: アプリケーションの種類は必ず「**デスクトップアプリ**」を選択してください。

1. 「認証情報を作成」→「OAuth クライアント ID」をクリック
2. アプリケーションの種類：「**デスクトップアプリ**」を選択
   - ❌ ウェブアプリケーション（使用不可）
   - ❌ Android（使用不可）
   - ❌ iOS（使用不可）
   - ✅ **デスクトップアプリ**（これを選択）
3. 名前を入力（例：「SheetSync Unity」）
4. 「作成」をクリック

### 3. credentials.jsonのダウンロードと配置

1. 作成したクライアントIDの右側にある「ダウンロード」アイコン（⬇）をクリック
2. ダウンロードしたファイルの名前を確認：
   - 通常は `client_secret_xxxxx.json` のような名前
   - これを `credentials.json` にリネーム
3. 以下の場所に配置：
   ```
   [Unityプロジェクトのルート]/ProjectSettings/SheetSync/credentials.json
   ```

### 4. credentials.jsonの形式確認

正しい`credentials.json`の形式例：
```json
{
  "installed": {
    "client_id": "xxxxxx.apps.googleusercontent.com",
    "project_id": "your-project-id",
    "auth_uri": "https://accounts.google.com/o/oauth2/auth",
    "token_uri": "https://oauth2.googleapis.com/token",
    "auth_provider_x509_cert_url": "https://www.googleapis.com/oauth2/v1/certs",
    "client_secret": "GOCSPX-xxxxxxxxxxxxx",
    "redirect_uris": ["http://localhost"]
  }
}
```

**注意点**:
- 最外側のキーが `"installed"` であること（デスクトップアプリの場合）
- `"web"` というキーの場合は、ウェブアプリケーション用なので使用不可

### 5. よくある間違い

#### ❌ 間違い1: ウェブアプリケーション用のクライアントを使用
```json
{
  "web": {
    "client_id": "...",
    "client_secret": "..."
  }
}
```
→ デスクトップアプリ用に作り直す必要があります

#### ❌ 間違い2: APIキーをcredentials.jsonとして使用
```json
{
  "api_key": "AIzaSy..."
}
```
→ OAuth2クライアントIDが必要です

#### ❌ 間違い3: サービスアカウントキーを使用
```json
{
  "type": "service_account",
  "project_id": "...",
  "private_key_id": "..."
}
```
→ OAuth2クライアントID（デスクトップアプリ）が必要です

### 6. 再認証の手順

1. 既存のトークンをクリア：
   - Unity Editorで `Tools > SheetSync > Clear OAuth2 Token` を実行
2. 正しい`credentials.json`を配置後、再度認証：
   - `Tools > SheetSync > Update Records (OAuth2)` を開く
   - 「認証を開始」をクリック

## それでも解決しない場合

1. `ProjectSettings/SheetSync/`フォルダの権限を確認
2. Unityエディターを再起動
3. `credentials.json`のエンコーディングがUTF-8であることを確認
4. Google Cloud ConsoleでGoogle Sheets APIが有効になっていることを確認

## 関連ドキュメント
- [OAuth2_Setup_Guide.md](OAuth2_Setup_Guide.md) - 初期セットアップの詳細手順