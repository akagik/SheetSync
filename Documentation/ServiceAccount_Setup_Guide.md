# Google Sheets サービスアカウント認証 セットアップガイド

## 概要
サービスアカウント認証は、ユーザー操作なしで自動的にGoogle Sheetsにアクセスできる認証方式です。
CI/CDや自動化処理に最適です。

## OAuth2認証との違い

| 項目 | サービスアカウント | OAuth2（デスクトップ） |
|------|-------------------|---------------------|
| ユーザー操作 | 不要 | 初回認証時に必要 |
| トークン更新 | 自動 | 自動（ただし定期的な再認証が必要な場合あり） |
| 権限付与方法 | スプレッドシートに共有設定 | Googleアカウントでログイン |
| 適用場面 | 自動化、CI/CD、サーバー処理 | 個人利用、開発環境 |

## セットアップ手順

### 1. サービスアカウントの作成

1. [Google Cloud Console](https://console.cloud.google.com/iam-admin/serviceaccounts)にアクセス
2. プロジェクトを選択（または新規作成）
3. 「サービスアカウントを作成」をクリック
4. 以下の情報を入力：
   - サービスアカウント名：`SheetSync Service`（任意）
   - サービスアカウントID：自動生成されたものを使用
   - 説明：`Unity SheetSync用のサービスアカウント`（任意）
5. 「作成して続行」をクリック
6. ロールは設定不要（スプレッドシートの共有で権限を付与するため）
7. 「完了」をクリック

### 2. サービスアカウントキーの作成

1. 作成したサービスアカウントをクリック
2. 「キー」タブを選択
3. 「キーを追加」→「新しいキーを作成」
4. キーのタイプ：**JSON**を選択
5. 「作成」をクリック
6. JSONファイルが自動的にダウンロードされる

### 3. キーファイルの配置

1. ダウンロードしたJSONファイルを`service-account-key.json`にリネーム
2. 以下の場所に配置：
   ```
   [Unityプロジェクト]/ProjectSettings/SheetSync/service-account-key.json
   ```

### 4. Google Sheets APIの有効化

1. [Google Cloud Console](https://console.cloud.google.com/)に戻る
2. 「APIとサービス」→「ライブラリ」
3. 「Google Sheets API」を検索
4. 「有効にする」をクリック（既に有効な場合はスキップ）

### 5. スプレッドシートの共有設定

**重要**: サービスアカウントがスプレッドシートにアクセスするには、共有設定が必要です。

1. 対象のGoogleスプレッドシートを開く
2. 右上の「共有」ボタンをクリック
3. サービスアカウントのメールアドレスを追加
   - メールアドレスは`service-account-key.json`内の`client_email`フィールドに記載
   - 例：`sheetsync-service@your-project-id.iam.gserviceaccount.com`
4. 権限を「編集者」に設定
5. 「送信」をクリック

## Unity内での使用方法

### 1. 認証の実行

1. Unity Editorで`Tools > SheetSync > Update Records (Service Account)`を開く
2. 「認証を開始」ボタンをクリック
3. 「サービスアカウント認証に成功しました」と表示されれば完了

### 2. 更新機能の使用

1. ConvertSettingを選択
2. 検索条件を入力（例：humanId = 1）
3. 更新内容を入力（例：name → Tanaka）
4. 「更新を実行」をクリック

## service-account-key.jsonの形式

正しいサービスアカウントキーの形式例：
```json
{
  "type": "service_account",
  "project_id": "your-project-id",
  "private_key_id": "key-id",
  "private_key": "-----BEGIN PRIVATE KEY-----\n...\n-----END PRIVATE KEY-----\n",
  "client_email": "sheetsync-service@your-project-id.iam.gserviceaccount.com",
  "client_id": "123456789",
  "auth_uri": "https://accounts.google.com/o/oauth2/auth",
  "token_uri": "https://oauth2.googleapis.com/token",
  "auth_provider_x509_cert_url": "https://www.googleapis.com/oauth2/v1/certs",
  "client_x509_cert_url": "https://www.googleapis.com/robot/v1/metadata/x509/..."
}
```

## トラブルシューティング

### 「service-account-key.jsonが見つかりません」エラー

1. ファイルが正しい場所に配置されているか確認
2. ファイル名が正確に`service-account-key.json`であることを確認

### 「認証に失敗しました」エラー

1. サービスアカウントキーが有効か確認
2. Google Sheets APIが有効になっているか確認
3. JSONファイルの形式が正しいか確認

### 「Permission denied」エラー

1. **スプレッドシートがサービスアカウントと共有されているか確認**
2. 共有時の権限が「編集者」になっているか確認
3. サービスアカウントのメールアドレスが正しいか確認

### 「API has not been used in project」エラー

1. Google Cloud ConsoleでSheets APIが有効になっているか確認
2. 正しいプロジェクトでAPIを有効にしているか確認

## セキュリティに関する注意

- `service-account-key.json`には秘密鍵が含まれます
- **必ず`.gitignore`に追加してください**
- チーム開発では、各開発者が独自のサービスアカウントを作成することを推奨
- 本番環境では、より厳格な権限管理を検討してください

## メリットとデメリット

### メリット
- ユーザー操作が不要（完全自動化可能）
- トークンの有効期限を気にする必要がない
- CI/CD環境での利用が容易
- 複数の環境で同じ認証情報を使用可能

### デメリット
- スプレッドシートごとに共有設定が必要
- サービスアカウントのメールアドレスが共有履歴に残る
- 秘密鍵の管理が必要

## 関連リンク

- [サービスアカウントの概要](https://cloud.google.com/iam/docs/service-accounts)
- [Google Sheets API ドキュメント](https://developers.google.com/sheets/api)
- [OAuth2認証ガイド](OAuth2_Setup_Guide.md)（別の認証方式）