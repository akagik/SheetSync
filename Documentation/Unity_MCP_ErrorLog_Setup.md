# Unity MCP サーバーを使用した Claude からのエラーログ取得手順

## 概要

このドキュメントでは、[mcp-unity](https://github.com/CoderGamester/mcp-unity) を使用して、Claude から Unity のエラーログを取得し、自動的にエラーを修正できるようにするセットアップ手順を説明します。

## 目的

- Claude から Unity のコンパイルエラーログを取得する
- エラー内容を自動的に分析し、修正案を提示または自動修正する
- Unity エディタを手動でアクティブにすることなくコンパイルを実行する

## セットアップ手順

### 1. MCP Unity プラグインのインストール

1. [mcp-unity](https://github.com/CoderGamester/mcp-unity) をダウンロード
2. Unity プロジェクトにインポート
3. Unity エディタで MCP サーバーが起動していることを確認（通常は `localhost:8090`）

### 2. 強制リコンパイル機能の追加

Unity エディタがバックグラウンドでもコンパイルを実行できるように、以下のスクリプトを追加します：

```csharp
using UnityEditor;

public static class ForceRecompile
{
    [MenuItem("Tools/ForceScriptReload")]
    public static void ForceReload()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        UnityEngine.Debug.Log("RequestScriptReload 実行済み");
    }
}
```

### 3. Claude での使用方法

#### エラーログの取得

```python
# エラーログのみを取得
mcp__mcp-unity__get_console_logs(
    logType="error",
    limit=50,
    includeStackTrace=false  # トークン節約のため false 推奨
)
```

#### リコンパイルの実行

```python
# 強制リコンパイルを実行
mcp__mcp-unity__execute_menu_item(menuPath="Tools/ForceScriptReload")

# 3-10秒待機してコンパイル完了を待つ
sleep 3
```

#### 完全なワークフロー例

1. ファイルを編集してエラーを修正
2. リコンパイルを実行
3. 待機（3-10秒）
4. エラーログを取得
5. エラーがあれば再度修正して繰り返し

## 注意事項

### コンパイルエラーの取得について

- **初回はエラーが取得できない場合がある**: Unity エディタが非アクティブの状態では、最初のリコンパイルではエラーが検出されないことがあります
- **解決策**: 
  - 一度手動で Unity エディタをアクティブにする
  - または `Assets/Refresh` を実行してから `Tools/ForceScriptReload` を実行

### パフォーマンスの考慮事項

- `includeStackTrace=false` を使用してトークンを 80-90% 節約
- 必要な場合のみスタックトレースを有効化
- エラーログは `logType="error"` でフィルタリング

### トラブルシューティング

1. **エラーログが取得できない場合**
   - Unity エディタがアクティブになっているか確認
   - MCP サーバーが起動しているか確認（ログに `WebSocket server started` が表示される）
   - リコンパイル後、十分な待機時間を取っているか確認

2. **接続エラーが発生する場合**
   - Unity が再起動中の可能性があるため、少し待ってから再試行
   - ポート 8090 が使用可能か確認

## 活用例

### 自動エラー修正フロー

1. プロジェクト全体のコンパイルエラーを取得
2. エラーメッセージを解析
3. 該当ファイルを読み込み
4. エラー箇所を特定して修正
5. リコンパイルして確認
6. すべてのエラーが解消されるまで繰り返し

### バッチ処理

複数のファイルにまたがるエラーを効率的に処理：

1. すべてのエラーログを一度に取得
2. ファイルごとにグループ化
3. 各ファイルを順次修正
4. 最後に一括でリコンパイルして確認

## 今後の改善案

1. **より確実なコンパイル実行**
   - `CompilationPipeline.RequestScriptCompilation()` を使用した直接的なコンパイル要求
   - Unity エディタのフォーカス状態に依存しない実装

2. **エラー検出の高速化**
   - コンパイル完了イベントの検出
   - リアルタイムエラー通知

3. **自動修正の高度化**
   - 一般的なエラーパターンのデータベース化
   - AI による修正提案の精度向上