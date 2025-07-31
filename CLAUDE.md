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
- **meta ファイルは Unity によって自動生成されるので、勝手に作成しないこと。**



## 設計と共通化

コーディングを行った後は、既存コード・新規コードで共通化できる部分がないかを必ず確認する。
また、他にもコーディング違反をしていないかを確認する。

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

## MCP Unity Integration

This project has MCP Unity integration enabled, allowing AI assistants to interact with Unity Editor.

### Invoking Static Methods

You can call any static method in Unity using the `invoke_static_method` tool. This is useful for:

- Debugging and logging
- Creating GameObjects programmatically
- Modifying project settings
- Running custom utility methods

#### Basic Usage

```json
{
  "typeName": "FullyQualifiedTypeName",
  "methodName": "MethodName",
  "parameters": [
    {
      "type": "parameterType",
      "value": parameterValue
    }
  ]
}
```

#### Common Examples

1. **Debug Logging**

```json
{
  "typeName": "UnityEngine.Debug",
  "methodName": "Log",
  "parameters": [{"type": "string", "value": "Debug message here"}]
}
```

2. **Create GameObject**

```json
{
  "typeName": "UnityEngine.GameObject",
  "methodName": "CreatePrimitive",
  "parameters": [{"type": "PrimitiveType", "value": "Cube"}]
}
```

3. **Display Dialog**

```json
{
  "typeName": "UnityEditor.EditorUtility",
  "methodName": "DisplayDialog",
  "parameters": [
    {"type": "string", "value": "Title"},
    {"type": "string", "value": "Message"},
    {"type": "string", "value": "OK"}
  ]
}
```

#### Supported Parameter Types

- **Primitives**: string, int, float, double, bool, long
- **Unity Types**: Vector3 `{"x": 0, "y": 0, "z": 0}`, Vector2, Color `{"r": 1, "g": 0, "b": 0, "a": 1}`
- **Arrays**: string[], int[], float[]
- **Enums**: Use string value (e.g., "Cube" for PrimitiveType.Cube)
- **GameObject**: Use the GameObject's name as a string

#### Project-Specific Static Methods

[List any custom static utility methods in your project that might be useful]

Example:

```csharp
// If your project has utility methods like:
public static class GameUtils
{
    public static void ResetGameState() { /* ... */ }
    public static void LoadLevel(string levelName) { /* ... */ }
}
```

You can call them with:

```json
{
  "typeName": "GameUtils",
  "methodName": "LoadLevel",
  "parameters": [{"type": "string", "value": "MainMenu"}]
}
```

### Important Notes

1. **Type Names**: Always use fully qualified type names including namespace (e.g., `UnityEngine.Debug` not just `Debug`)
2. **Method Visibility**: Only public static methods can be invoked
3. **Return Values**: Methods that return values will include the result in the response
4. **Error Handling**: Check the response for error messages if a method call fails
5. **Unity Main Thread**: All methods are executed on Unity's main thread

### Common Tasks Using invoke_static_method

1. **Clear Console**

```json
{
  "typeName": "UnityEditorInternal.InternalEditorUtility",
  "methodName": "ClearConsoleWindow",
  "parameters": []
}
```

2. **Save Project**

```json
{
  "typeName": "UnityEditor.AssetDatabase",
  "methodName": "SaveAssets",
  "parameters": []
}
```

3. **Refresh Asset Database**

```json
{
  "typeName": "UnityEditor.AssetDatabase",
  "methodName": "Refresh",
  "parameters": []
}
```

4. **Set Player Preferences**

```json
{
  "typeName": "UnityEngine.PlayerPrefs",
  "methodName": "SetString",
  "parameters": [
    {"type": "string", "value": "KeyName"},
    {"type": "string", "value": "Value"}
  ]
}
```

### Debugging Tips

- Use `Debug.Log` to output values and track execution
- Check Unity Console for any error messages
- Verify type names and method signatures match exactly
- Remember that Unity must be running with MCP Unity server active

### Advanced Usage

#### Working with Complex Types

For methods that require complex Unity types:

```json
{
  "typeName": "UnityEngine.GameObject",
  "methodName": "Find",
  "parameters": [{"type": "string", "value": "/Canvas/Button"}]
}
```

#### Chaining Operations

While you cannot chain method calls directly, you can use multiple tool invocations:

1. First, create an object:

```json
{
  "typeName": "UnityEngine.GameObject",
  "methodName": "CreatePrimitive",
  "parameters": [{"type": "PrimitiveType", "value": "Sphere"}]
}
```

2. Then modify it using other tools or methods:

```json
{
  "typeName": "UnityEngine.GameObject",
  "methodName": "Find",
  "parameters": [{"type": "string", "value": "Sphere"}]
}
```

#### Error Recovery

If a method call fails:

1. Check the exact type name spelling (case-sensitive)
2. Verify the method is public and static
3. Ensure parameter types match exactly
4. Check Unity Console for detailed error messages

### Limitations

- Only static methods can be invoked (no instance methods)
- Cannot access private or internal methods
- Complex object parameters may need to be passed as simpler types
- Some Unity Editor operations may require specific editor states

### Security Considerations

This tool can execute any public static method. In production:

- Consider restricting which types/methods can be called
- Log all method invocations for audit purposes
- Be cautious with methods that modify project files or settings

# 📘 コーディングルール：責務の分離と共通処理の抽出について

## ✅ ルール概要

**長大なメソッド内に複数の責務が混在する場合は、適切に関数を分離し、再利用可能な処理は共通関数として抽出すること。**

### このルールが推奨される理由

- 🧩 **可読性向上**：1つのメソッドが明確な単位で読める
- 🔁 **再利用性向上**：似たような処理を他でも使い回せる
- 🛠️ **保守性向上**：バグ修正・仕様変更時の影響範囲が限定される
- 🧪 **テスト容易性**：分割した処理に対してユニットテストが書きやすい

------

## 📌 適用例：Google Sheets API を用いたデータ取得処理

### ❌ リファクタ前のコード（1つのメソッドにすべての処理が詰め込まれている）

```csharp
private static async Task<bool> DownloadAsDataInternalAsync(SheetDownloadInfo sheet, string apiKey)
{
    var service = new SheetsService(new BaseClientService.Initializer
    {
        ApiKey = apiKey,
        ApplicationName = "SheetSync"
    });

    // GID からシート名を取得（責務1）
    var spreadsheet = await service.Spreadsheets.Get(sheet.SheetId).ExecuteAsync();
    string sheetName = null;
    foreach (var s in spreadsheet.Sheets)
    {
        if (s.Properties.SheetId.ToString() == sheet.Gid)
        {
            sheetName = s.Properties.Title;
            break;
        }
    }

    if (string.IsNullOrEmpty(sheetName))
    {
        previousError = "該当するシートが見つかりません";
        return false;
    }

    // データ取得と格納（責務2）
    var response = await service.Spreadsheets.Values.Get(sheet.SheetId, sheetName).ExecuteAsync();
    if (response.Values == null || response.Values.Count == 0)
    {
        previousError = "データが空です";
        return false;
    }

    previousDownloadData = response.Values;
    return true;
}
```

------

### ✅ リファクタ後のコード（責務ごとに関数を分割し、共通化）

```csharp
private static async Task<bool> DownloadAsDataInternalAsync(SheetDownloadInfo sheet, string apiKey)
{
    var service = new SheetsService(new BaseClientService.Initializer
    {
        ApiKey = apiKey,
        ApplicationName = "SheetSync"
    });

    var sheetName = await GetSheetNameFromGidAsync(service, sheet.SheetId, sheet.Gid);
    if (string.IsNullOrEmpty(sheetName))
    {
        previousError = $"GID '{sheet.Gid}' に対応するシートが見つかりません。";
        return false;
    }

    var response = await service.Spreadsheets.Values.Get(sheet.SheetId, sheetName).ExecuteAsync();
    if (response.Values == null || response.Values.Count == 0)
    {
        previousError = $"スプレッドシート '{sheetName}' にデータがありません。";
        return false;
    }

    previousDownloadData = response.Values;
    return true;
}

private static async Task<string> GetSheetNameFromGidAsync(SheetsService service, string sheetId, string gid)
{
    var spreadsheet = await service.Spreadsheets.Get(sheetId).ExecuteAsync();
    foreach (var sheet in spreadsheet.Sheets)
    {
        if (sheet.Properties.SheetId.ToString() == gid)
            return sheet.Properties.Title;
    }
    return null;
}
```

------

## 🧠 補足アドバイス（AI向け）

- **「一目で処理の意図がわからないブロック」**がある場合は、切り出しの候補。
- **2箇所以上で使われる可能性のあるロジック**は積極的に共通関数化を検討。
- **呼び出し箇所が1つでも、「再利用の可能性 + テスト容易性 + 責務の明確化」**の観点で分割を正当化できるなら、迷わず抽出してよい。


# important-instruction-reminders
Do what has been asked; nothing more, nothing less.
NEVER create files unless they're absolutely necessary for achieving your goal.
ALWAYS prefer editing an existing file to creating a new one.
NEVER proactively create documentation files (*.md) or README files. Only create documentation files if explicitly requested by the User.

## SheetSync 外部API

SheetSync には、Unity外部から操作するための静的APIが実装されています。

### SheetSyncApi

`SheetSync.Api.SheetSyncApi` クラスは、MCP経由でAIや外部システムから呼び出すための静的メソッドを提供します。

#### 主な機能

1. **認証管理**
   - `InitializeAuth(string credentialsPath)` - サービスアカウント認証の初期化
   - `CheckAuthStatus()` - 認証状態の確認

2. **データ更新**
   - `UpdateRow(string requestJson)` - 単一行の更新
   - `UpdateMultipleRows(string requestJson)` - 複数行の一括更新

3. **ユーティリティ**
   - `GetApiInfo()` - API情報の取得
   - `GetSampleUpdateRequest()` - サンプルリクエストの取得
   - `GetSampleBatchUpdateRequest()` - バッチ更新サンプルの取得

#### MCP経由での呼び出し例

```json
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

#### 設計原則

- すべてのメソッドは静的メソッド
- 複雑なデータはJSON文字列として受け渡し
- 統一されたApiResponse構造でエラーハンドリング
- 非同期処理を同期的に実行してタイムアウト対応

詳細は `/Docs/SheetSyncApi.md` を参照してください。

