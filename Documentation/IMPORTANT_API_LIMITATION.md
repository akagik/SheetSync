# Google Sheets API の重要な制限事項

## 問題
Google Sheets API v4 では、以下の制限があります：

- **読み取り操作**: APIキーで可能（公開されているスプレッドシートのみ）
- **書き込み操作**: OAuth2認証が必須（APIキーでは不可）

## エラーメッセージ
```
API keys are not supported by this API. Expected OAuth2 access token or other authentication credentials that assert a principal.
```

## 対応方法

### 1. OAuth2認証の実装（推奨）
Google OAuth2認証を実装することで、読み取り・書き込みの両方が可能になります。
ただし、実装が複雑になります。

### 2. 読み取り専用として使用
現在のAPIキー方式では、以下の操作のみ可能です：
- スプレッドシートからのデータ読み取り
- データのローカル処理
- ScriptableObjectへの変換

### 3. Google Apps Script を使用（代替案）
Google Apps Script でWeb APIを作成し、そこから更新を行う方法もあります。

## 現在の実装の制限
SheetUpdateService は設計上は更新機能を持っていますが、APIキー認証では実行できません。
OAuth2認証の実装が必要です。