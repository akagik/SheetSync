# Google Sheets API v4 Unity セットアップガイド

## 概要

このドキュメントでは、SheetSync パッケージで Google Sheets API v4 公式 C# クライアントライブラリを使用するためのセットアップ手順を説明します。SheetSync は Unity Package Manager (UPM) で配布される自作パッケージであるため、通常の Unity プロジェクトとは異なる注意点があります。

## 前提条件

- Unity 2020.3 以降
- .NET Standard 2.1 対応
- Google Cloud Platform アカウント

## セットアップ手順

### 1. Google Cloud Platform での準備

#### 1.1 Google Cloud Console でプロジェクトを作成

1. [Google Cloud Console](https://console.cloud.google.com/) にアクセス
2. 新しいプロジェクトを作成、または既存のプロジェクトを選択
3. プロジェクトIDをメモしておく

#### 1.2 Google Sheets API を有効化

1. ナビゲーションメニューから「APIとサービス」→「ライブラリ」を選択
2. 「Google Sheets API」を検索
3. 「有効にする」をクリック

#### 1.3 認証情報の作成

**APIキーを使用する場合（読み取り専用、公開データ）：**

1. 「APIとサービス」→「認証情報」を選択
2. 「+ 認証情報を作成」→「APIキー」を選択
3. 作成されたAPIキーをコピー
4. 必要に応じてAPIキーに制限を設定（推奨）
   - アプリケーションの制限: IPアドレスまたはHTTPリファラー
   - APIの制限: Google Sheets API のみに制限

**サービスアカウントを使用する場合（読み書き可能、プライベートデータ）：**

1. 「APIとサービス」→「認証情報」を選択
2. 「+ 認証情報を作成」→「サービスアカウント」を選択
3. サービスアカウント名と説明を入力
4. 作成されたサービスアカウントをクリック
5. 「キー」タブ→「鍵を追加」→「新しい鍵を作成」
6. JSON形式を選択してダウンロード
7. このJSONファイルを安全に保管

### 2. Unity プロジェクトへのライブラリ導入

⚠️ **重要: SheetSync パッケージ利用時の注意事項**

SheetSync は Unity パッケージとして提供されているため、依存ライブラリの管理には特別な配慮が必要です。

#### パッケージ依存関係の管理方法

**方法1: メインプロジェクトでの管理（推奨）**
- Google API ライブラリは、SheetSync を使用するメインプロジェクト側でインストール
- これにより、複数のパッケージ間での依存関係の競合を回避

**方法2: パッケージ内での管理**
- `Packages/SheetSync/package.json` に依存関係を記述
- ただし、NuGet パッケージは直接指定できないため、別途配布が必要

#### 2.1 NuGetForUnity のインストール（推奨方法）

1. Unity Package Manager から NuGetForUnity をインストール
   ```
   Window → Package Manager → + → Add package from git URL...
   https://github.com/GlitchEnzo/NuGetForUnity.git?path=/src/NuGetForUnity
   ```

2. インストール後、メニューに「NuGet」が追加されます

#### 2.2 Google Sheets API パッケージのインストール

1. Unity メニューから「NuGet」→「Manage NuGet Packages」を選択
2. 以下のパッケージを検索してインストール：
   - `Google.Apis.Sheets.v4` (最新版)
   - `Google.Apis.Auth` (依存関係として自動インストール)

#### 2.3 手動インストール（NuGetForUnityが使えない場合）

1. 以下のDLLをダウンロード：
   - Google.Apis.dll
   - Google.Apis.Core.dll
   - Google.Apis.Auth.dll
   - Google.Apis.Auth.PlatformServices.dll
   - Google.Apis.Sheets.v4.dll
   - Newtonsoft.Json.dll (Unity 2020.3以降は不要)

2. ダウンロードしたDLLを配置：
   - **メインプロジェクトの場合**: `Assets/Plugins/Google/` フォルダに配置
   - **SheetSync パッケージ内の場合**: `Packages/SheetSync/Runtime/Plugins/Google/` フォルダに配置（非推奨）

### 3. 設定ファイルの作成

⚠️ **パッケージ利用時の設定ファイル管理**

SheetSync パッケージを使用する場合、設定ファイルは以下の場所に作成することを推奨：

- **推奨**: `Assets/SheetSyncSettings/` フォルダ（メインプロジェクト側）
- **非推奨**: `Packages/SheetSync/` 内（パッケージ更新時に失われる可能性）

#### 3.1 Google API 設定用 ScriptableObject

```csharp
using UnityEngine;

namespace SheetSync.Editor.Google
{
    [CreateAssetMenu(menuName = "SheetSync/GoogleAPISettings", fileName = "GoogleAPISettings")]
    public class GoogleAPISettings : ScriptableObject
    {
        [Header("認証設定")]
        [Tooltip("API キー（公開データの読み取り専用）")]
        public string apiKey;
        
        [Header("サービスアカウント設定（プライベートデータ用）")]
        [Tooltip("サービスアカウントのJSONファイルパス")]
        public string serviceAccountJsonPath;
        
        [Header("その他の設定")]
        [Tooltip("アプリケーション名")]
        public string applicationName = "SheetSync Unity Application";
        
        [Tooltip("タイムアウト時間（秒）")]
        public int timeoutSeconds = 30;
    }
}
```

### 4. Unity パッケージとしての使用上の注意

#### 4.1 Assembly Definition の設定

SheetSync パッケージは Assembly Definition ファイル (asmdef) を使用しています：

```json
// Packages/SheetSync/Editor/SheetSync.Editor.asmdef
{
    "name": "SheetSync.Editor",
    "references": [
        "Google.Apis",
        "Google.Apis.Sheets.v4"
    ],
    "includePlatforms": ["Editor"],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": []
}
```

**重要**: Google API の DLL がメインプロジェクトにある場合、asmdef の参照設定に注意が必要です。

#### 4.2 パッケージの配布と依存関係

**package.json での依存関係記述例**:
```json
{
    "name": "com.yourcompany.sheetsync",
    "version": "1.0.0",
    "displayName": "SheetSync",
    "dependencies": {
        "com.unity.nuget.newtonsoft-json": "3.0.2"
    },
    "documentationUrl": "https://your-docs-url.com",
    "changelogUrl": "https://your-changelog-url.com",
    "licensesUrl": "https://your-license-url.com"
}
```

**注意**: Google API ライブラリは NuGet パッケージのため、package.json では直接指定できません。

#### 4.3 依存ライブラリの同梱について

**同梱する場合の注意点**:
1. ライセンスの確認（Apache License 2.0）
2. バージョン競合の可能性
3. パッケージサイズの増大
4. 更新時のメンテナンス負担

**推奨アプローチ**:
- README やセットアップガイドで必要なライブラリを明記
- セットアップスクリプトやウィザードを提供
- NuGetForUnity の使用を前提とした設計

#### 4.4 スレッドの問題

Google API ライブラリは非同期処理を使用しますが、Unity のメインスレッドとの互換性に注意が必要です：

```csharp
// Unity のメインスレッドで実行する必要がある処理
UnityMainThreadDispatcher.Instance().Enqueue(() => {
    // UI更新など
});
```

#### 4.5 プラットフォーム固有の問題

- **iOS**: IL2CPP ビルド時に link.xml の設定が必要な場合があります
- **Android**: ProGuard 使用時は、Google API 関連クラスを除外設定に追加
- **WebGL**: 現在、WebGL では Google API クライアントライブラリは動作しません

#### 4.6 エディタ専用機能として実装

SheetSync はエディタ機能として実装することを推奨：

```csharp
#if UNITY_EDITOR
using Google.Apis.Sheets.v4;
using Google.Apis.Services;
// エディタ専用コード
#endif
```

### 5. パッケージ特有のトラブルシューティング

#### 5.1 パッケージ関連のエラー

**エラー: "Assembly 'Google.Apis' not found"**
- 原因: Google API ライブラリがインストールされていない
- 対処: 
  1. メインプロジェクトで NuGetForUnity を使用してインストール
  2. または、手動で DLL を `Assets/Plugins/` に配置

**エラー: "Multiple precompiled assemblies with the same name"**
- 原因: Google API ライブラリが複数の場所に存在
- 対処: 
  1. パッケージ内とプロジェクト内の重複を確認
  2. どちらか一方のみに統一

**エラー: "The type or namespace name 'Google' could not be found"**
- 原因: Assembly Definition の参照設定が不適切
- 対処:
  1. asmdef ファイルの references を確認
  2. Google API の DLL が正しい場所にあることを確認

### 6. パッケージ開発者向けガイドライン

#### 6.1 ベストプラクティス

1. **依存関係の文書化**
   - README.md に必要なライブラリとバージョンを明記
   - セットアップ手順を詳細に記載

2. **セットアップの自動化**
   ```csharp
   [InitializeOnLoadMethod]
   static void CheckDependencies()
   {
       if (!IsGoogleApisInstalled())
       {
           EditorUtility.DisplayDialog(
               "SheetSync Setup",
               "Google Sheets API ライブラリがインストールされていません。\n" +
               "Window > SheetSync > Setup Guide を参照してください。",
               "OK"
           );
       }
   }
   ```

3. **バージョン互換性の管理**
   - 対応する Unity バージョンを明記
   - Google API ライブラリの推奨バージョンを指定

#### 6.2 よくあるエラーと対処法

**エラー: "Google.Apis.Json.JsonReaderException"**
- 原因: Newtonsoft.Json のバージョン競合
- 対処: Unity 2020.3以降の内蔵 Newtonsoft.Json を使用

**エラー: "System.Net.Http.HttpRequestException"**
- 原因: ネットワーク接続またはプロキシ設定
- 対処: プロキシ設定を確認、またはファイアウォール設定を調整

**エラー: "Google.GoogleApiException: Insufficient Permission"**
- 原因: APIキーまたはサービスアカウントの権限不足
- 対処: Google Cloud Console で権限を確認・更新

### 7. セキュリティのベストプラクティス

1. **APIキーの保護**
   - APIキーをソースコードに直接記述しない
   - 環境変数または暗号化された設定ファイルを使用
   - .gitignore に設定ファイルを追加

2. **サービスアカウントキーの管理**
   - JSONファイルをプロジェクトに含めない
   - StreamingAssets フォルダーの外部に配置
   - 必要最小限の権限のみ付与

3. **本番環境での使用**
   - APIキーに適切な制限を設定
   - 使用量の監視とアラート設定
   - 定期的なキーのローテーション

### 8. パッケージ利用者向けクイックスタート

1. **必要なツールのインストール**
   ```
   # Package Manager で NuGetForUnity をインストール
   Window → Package Manager → + → Add package from git URL
   https://github.com/GlitchEnzo/NuGetForUnity.git?path=/src/NuGetForUnity
   ```

2. **Google API ライブラリのインストール**
   ```
   NuGet → Manage NuGet Packages
   → "Google.Apis.Sheets.v4" を検索してインストール
   ```

3. **設定ファイルの作成**
   ```
   Assets フォルダで右クリック
   → Create → SheetSync → GoogleAPISettings
   ```

4. **API キーの設定**
   ```
   作成した GoogleAPISettings を選択
   → Inspector で API Key を入力
   ```

5. **動作確認**
   ```
   Window → SheetSync → Google API Setup
   → "設定をテスト" ボタンをクリック
   ```

## 次のステップ

セットアップが完了したら、`GoogleSheetsService.cs` を参照して実際の実装を開始してください。

## 関連ドキュメント

- [SheetSync README](../README.md)
- [Google Sheets API 公式ドキュメント](https://developers.google.com/sheets/api)
- [Google API .NET クライアントライブラリ](https://github.com/googleapis/google-api-dotnet-client)