# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

このプロジェクトは KoheiUtils の CsvConverter をコピーした新しいモジュールの Sheet Sync です。
スプレッドシートAPIを使い、指定のスプレッドシートから Scriptable Object に変換したり、逆にスプレッドシート側を更新することを可能にします。

### プロジェクト構造
- **Unity Package**: UPM (Unity Package Manager) 形式のパッケージ
- **Package ID**: `com.kc-works.sheet-sync`
- **Unity Version**: 2022.3.25以上
- **アセンブリ定義**:
  - `Kohei.SheetSync.Editor` - メインのエディター機能
  - `Kohei.SheetSync.Tests.Editor` - テストコード

### Kohei Utils からの移行

CsvConverterSettings や GlobalCCSettings などは本来でいうと SheetSync に配置したいが、互換性を保つために KoheiUtils のものを使って、 SheetSync からは除外しています。
将来的には廃止して、 SheetSync 由来のものに置き換えたいです。

## 開発環境

- **Unity Version**: 2022.3.25以上
- **MCP Unity Server**: [mcp-unity](https://github.com/CoderGamester/mcp-unity) を使用
- **ポート**: localhost:8090

## Development Commands

### Unity エディター内での操作
```
# テストの実行
Window > General > Test Runner を開き、"Kohei.SheetSync.Tests.Editor" のテストを実行

# パッケージの更新
Window > Package Manager > "+" > Add package from disk... > package.json を選択

# CSV Converter ウィンドウを開く
Tools > SheetSync > CSV Converter
```

### Git コマンド
```bash
# ステータス確認
git status

# 変更内容の確認
git diff

# コミット (日本語メッセージで)
git add .
git commit -m "機能: CSVからScriptableObjectへの変換処理を実装"

# タグ付きリリース
git tag v0.0.1
git push origin main --tags
```

## High-level Architecture

### コア機能の構成

1. **CSV/スプレッドシート → ScriptableObject 変換**
   - `CsvConverter.cs` - メインの変換ロジック
   - `Logic/CsvLogic.cs` - CSV解析とデータ処理
   - `ConvertSetting.cs` - 変換設定の管理

2. **コード生成システム**
   - `ClassGenerator.cs` - ScriptableObjectクラスの自動生成
   - `EnumGenerator.cs` - Enumの自動生成
   - `AssetsGenerator.cs` - ScriptableObjectアセットの生成
   - `Templates/` - コード生成用テンプレート

3. **エディターUI**
   - `CsvConverterWindow.cs` - メインのエディターウィンドウ
   - `CCSettingsEditWindow.cs` - 設定編集ウィンドウ
   - 今後: スプレッドシート同期UI

### データフロー
```
CSV/スプレッドシート
    ↓ (読み込み)
CsvLogic で解析
    ↓ (スキーマ解析)
ClassGenerator でC#コード生成
    ↓ (コンパイル)
AssetsGenerator でScriptableObject生成
    ↓ (保存)
Unityプロジェクトのアセット
```

### 今後の拡張予定
- Google Sheets API 統合
- 双方向同期 (ScriptableObject → スプレッドシート)
- リアルタイム同期機能

## Development Workflow

### Git Branches
- **main** - メインブランチ（プルリクエスト用）
- **master** - レガシーブランチ（使用しない）

### 名前空間の移行状況
現在 `KoheiUtils` から `SheetSync` への移行中:
- [ ] namespace の変更
- [ ] ファイル名の変更
- [ ] アセンブリ定義の更新
- [ ] テストコードの更新

## アーキテクチャガイドライン

### 主要な設計原則

1. **長期的な保守性** - コードの整理とコンポーネント分割を最優先
2. **ファイル分割** - 大きなコンポーネントは小さく管理しやすいファイルに分割
3. **APIファーストアプローチ** - 再利用可能なAPIとして実装
4. **日本語ドキュメント** - すべてのドキュメントは日本語で作成

## 重要な注意事項

### Unity 特有の注意点
- **メタファイル**: `.meta` ファイルは必ずコミットに含める
- **エディター専用**: すべてのコードは Editor フォルダ内に配置
- **ScriptableObject**: 生成されたアセットはプロジェクトに依存

### 長期運用のための設計指針

- **保守性重視**: 後からメンテナンスしやすいように、設計とコンポーネント分割には細心の注意を払う
- **積極的なファイル分割**: ファイルは可能な限り分割し、各ファイルは単一の責任を持つように設計
- **明確な命名規則**: ファイル名、関数名、変数名は日本語でも理解しやすい名前を使用
- **型安全性**: C#の型システムを最大限活用し、型安全性を確保

### Git管理のルール

- **こまめなコミット**: ファイル操作を行った場合は必ずコミットを作成
- **わかりやすいコミットメッセージ**: 日本語で具体的な変更内容を記述
- **メタファイルの同期**: Unityのメタファイルは必ず一緒にコミット

### ドキュメント作成ガイドライン

- **日本語優先**: すべてのドキュメントは日本語で作成
- **詳細な説明**: 実装の意図や使用方法を明確に記述
- **サンプルコード**: 可能な限り使用例を含める

### コンポーネント設計の原則

- **疎結合**: コンポーネント間の依存関係を最小限に
- **高凝集**: 関連する機能は同じコンポーネントに
- **インターフェース定義**: コンポーネント間の通信は明確なインターフェースを通じて行う

## 開発時の注意点

- 移行作業中: KoheiUtils から SheetSync への名前空間変更が進行中
- 詳細な仕様については `AI_INSTRUCTIONS.md` の要件に従ってください
- Git でバージョン管理を行います
- **重要**: ソースコード編集後は必ずコンパイルエラーが発生しないことを確認する

## エラー修正ワークフロー

### 基本的な流れ

**ソースコード編集後は必ずコンパイルエラーチェックを実施**

1. **ファイル編集後は必ずリコンパイル**
   
   ```
   mcp__mcp-unity__execute_menu_item(menuPath="Tools/ForceScriptReload")
   ```
   
2. **コンパイル完了を待機**
   - 3-5 秒待機

3. **エラーログを取得**
   ```
   mcp__mcp-unity__get_console_logs(
       logType="error",
       limit=50,
       includeStackTrace=false
   )
   ```

4. **エラーがある場合は自動修正**
   - エラーメッセージからファイルと行番号を特定
   - 該当箇所を読み込んで修正
   - 再度リコンパイルして確認（手順1に戻る）

5. **エラーがなくなるまで繰り返し**

### エラー修正の具体例

タスク: プロジェクト全体のコンパイルエラーを修正

1. リコンパイルを実行してエラーを検出
2. 各エラーについて：
   - ファイルパスと行番号を抽出（例: `SheetSync.cs(35,76): error CS1002`）
   - 該当ファイルを読み込み
   - エラー内容に基づいて修正
3. すべて修正後、再度リコンパイルして確認
4. エラーがなくなるまで繰り返し

### **必須注意事項**

**コミット前確認**: Git コミット前は必ずコンパイルエラーがないことを確認

## 作業ログの管理

### 作業ログの基本ルール

**重要**: すべての対話の最後に、必ず以下の手順に従って作業ログをファイルに出力してください。

1. **保存先**: 作業ログはすべてプロジェクトルート直下の `./worklogs/` ディレクトリに保存
   - ディレクトリが存在しない場合は `mkdir -p ./worklogs/` で作成

2. **ファイル名**: `{連番}_具体的なトピック.md` の形式で命名
   - 関連する作業は同じファイルに追記

3. **書き込み形式**: 後述の「作業ログフォーマット」を厳守

### ワークフロー

1. ユーザーから指示を受け取る
2. 必要に応じて過去の `./worklogs/` 内のログを参照し、文脈を理解
3. 指示内容を分析し、必要なコマンド実行やファイル編集を実行
4. **ファイル操作を行った場合は必ずコミットを作成**
5. 作業完了後、作業ログファイルを作成または追記
6. 一連の作業が完了したことを報告

### パッケージのバージョン更新

- 既存のバージョンを確認して、バッチ番号を1つインクリメントする
- package.json の version を更新する
- CHANGELOG.md を更新する
- commit して version タグ `v3.0.12` のようなものをつけて一緒にプッシュする.

### 作業ログフォーマット

```markdown
---
**【指示】**
> (ユーザーからの指示プロンプトを引用)

**【作業記録】**
今回の指示に対する思考プロセス、実行した手順、生成したコードやコマンド、そして考察を記述。
後から見て作業の流れが完全に理解できるように、詳細かつ分かりやすく記録。
箇条書きやコードブロックを効果的に使用。
```

