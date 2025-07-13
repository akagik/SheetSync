# Google Sheets OAuth2認証 セットアップガイド

## 概要
このガイドでは、SheetSyncでGoogle Sheetsの更新機能を使用するためのOAuth2認証の設定方法を説明します。

## 前提条件
- Googleアカウント
- Google Cloud Consoleへのアクセス

## セットアップ手順

### 1. Google Cloud Projectの作成

1. [Google Cloud Console](https://console.cloud.google.com/)にアクセス
2. 新しいプロジェクトを作成（または既存のプロジェクトを選択）

### 2. Google Sheets APIの有効化

1. 左側のメニューから「APIとサービス」→「ライブラリ」を選択
2. 「Google Sheets API」を検索
3. 「有効にする」をクリック

### 3. OAuth2認証情報の作成

1. 「APIとサービス」→「認証情報」を選択
2. 「認証情報を作成」→「OAuth クライアント ID」を選択
3. アプリケーションの種類：「デスクトップアプリ」を選択
4. 名前を入力（例：「SheetSync」）
5. 「作成」をクリック

### 4. credentials.jsonのダウンロード

1. 作成したOAuth2クライアントの右側の「ダウンロード」アイコンをクリック
2. ダウンロードしたファイルを`credentials.json`にリネーム
3. 以下の場所に配置：
   ```
   [Unityプロジェクト]/ProjectSettings/SheetSync/credentials.json
   ```

### 5. OAuth同意画面の設定（初回のみ）

1. 「APIとサービス」→「OAuth同意画面」を選択
2. ユーザータイプ：「外部」を選択（組織内のみの場合は「内部」）
3. 必要な情報を入力：
   - アプリ名
   - サポートメール
   - デベロッパーの連絡先
4. スコープの追加：
   - `https://www.googleapis.com/auth/spreadsheets`
5. テストユーザーに自分のGoogleアカウントを追加

## Unity内での使用方法

### 1. 認証の実行

1. Unity Editorで`Tools > SheetSync > Update Records (OAuth2)`を開く
2. 「認証を開始」ボタンをクリック
3. ブラウザが開き、Googleアカウントでログイン
4. 権限を許可
5. 「認証に成功しました」と表示されれば完了

### 2. 更新機能の使用

1. ConvertSettingを選択
2. 検索条件を入力（例：humanId = 1）
3. 更新内容を入力（例：name → Tanaka）
4. 「更新を実行」をクリック

## トラブルシューティング

### 「credentials.jsonが見つかりません」エラー

1. ファイルが正しい場所に配置されているか確認
2. ファイル名が正確に`credentials.json`であることを確認

### 「認証に失敗しました」エラー

1. インターネット接続を確認
2. Google Cloud ConsoleでAPIが有効になっているか確認
3. OAuth同意画面の設定が完了しているか確認

### 「スコープが不足しています」エラー

1. OAuth同意画面でspreadsheets scopeが追加されているか確認
2. 既存のトークンをクリア（`Tools > SheetSync > Clear OAuth2 Token`）
3. 再度認証を実行

## セキュリティに関する注意

- `credentials.json`には機密情報が含まれます
- `.gitignore`に追加することを推奨
- 本番環境では、より安全な認証方法の使用を検討してください

## 関連リンク

- [Google Sheets API ドキュメント](https://developers.google.com/sheets/api)
- [OAuth2.0 の概要](https://developers.google.com/identity/protocols/oauth2)