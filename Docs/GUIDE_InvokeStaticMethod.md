# Invoke Static Method Tool ガイド

`invoke_static_method`ツールを使用すると、任意のstaticメソッドを引数付きで呼び出すことができます。

## 基本的な使い方

### 構文

```json
{
  "typeName": "完全修飾型名",
  "methodName": "メソッド名",
  "parameters": [
    {
      "type": "パラメータ型",
      "value": パラメータ値
    }
  ]
}
```

## 使用例

### 1. Unity Debug.Log の呼び出し

```json
{
  "typeName": "UnityEngine.Debug",
  "methodName": "Log",
  "parameters": [
    {
      "type": "string",
      "value": "Hello from MCP Unity!"
    }
  ]
}
```

### 2. GameObject の作成

```json
{
  "typeName": "UnityEngine.GameObject",
  "methodName": "CreatePrimitive",
  "parameters": [
    {
      "type": "PrimitiveType",
      "value": "Cube"
    }
  ]
}
```

### 3. PlayerPrefs の操作

```json
{
  "typeName": "UnityEngine.PlayerPrefs",
  "methodName": "SetString",
  "parameters": [
    {
      "type": "string",
      "value": "PlayerName"
    },
    {
      "type": "string",
      "value": "MCP User"
    }
  ]
}
```

### 4. EditorUtility ダイアログの表示

```json
{
  "typeName": "UnityEditor.EditorUtility",
  "methodName": "DisplayDialog",
  "parameters": [
    {
      "type": "string",
      "value": "MCP Unity"
    },
    {
      "type": "string",
      "value": "このダイアログはMCP経由で作成されました！"
    },
    {
      "type": "string",
      "value": "OK"
    }
  ]
}
```

### 5. Vector3 パラメータの使用

```json
{
  "typeName": "YourNamespace.YourClass",
  "methodName": "SetPosition",
  "parameters": [
    {
      "type": "Vector3",
      "value": {
        "x": 10.5,
        "y": 2.0,
        "z": -5.3
      }
    }
  ]
}
```

### 6. 配列パラメータの使用

```json
{
  "typeName": "YourNamespace.StringProcessor",
  "methodName": "ProcessStrings",
  "parameters": [
    {
      "type": "string[]",
      "value": ["first", "second", "third"]
    }
  ]
}
```

### 7. 複数の引数を持つメソッド

```json
{
  "typeName": "UnityEngine.Mathf",
  "methodName": "Clamp",
  "parameters": [
    {
      "type": "float",
      "value": 15.7
    },
    {
      "type": "float",
      "value": 0.0
    },
    {
      "type": "float",
      "value": 10.0
    }
  ]
}
```

## サポートされている型

### 基本型

- `string` - 文字列
- `int` / `int32` - 32ビット整数
- `float` / `single` - 単精度浮動小数点数
- `double` - 倍精度浮動小数点数
- `bool` / `boolean` - ブール値
- `long` / `int64` - 64ビット整数

### Unity型

- `Vector2` - 2次元ベクトル `{"x": 0, "y": 0}`
- `Vector3` - 3次元ベクトル `{"x": 0, "y": 0, "z": 0}`
- `Color` - 色 `{"r": 1, "g": 0, "b": 0, "a": 1}`
- `GameObject` - GameObjectの名前を文字列で指定

### 配列型

- `string[]` - 文字列配列
- `int[]` - 整数配列
- `float[]` - 浮動小数点数配列

### 列挙型

列挙型は文字列として値を指定します：

```json
{
  "type": "PrimitiveType",
  "value": "Cube"
}
```

## 非同期メソッドのサポート

### async/await メソッドの呼び出し

InvokeStaticMethodToolは、`Task`、`Task<T>`、`UniTask`、`UniTask<T>`を返す非同期メソッドもサポートしています。

#### Task を返すメソッド

```csharp
// C# コード例
public static class AsyncOperations
{
    public static async Task DelayedLogAsync(string message, int delayMs)
    {
        await Task.Delay(delayMs);
        Debug.Log($"[Delayed] {message}");
    }
}
```

MCP経由での呼び出し：

```json
{
  "typeName": "AsyncOperations",
  "methodName": "DelayedLogAsync",
  "parameters": [
    {
      "type": "string",
      "value": "Hello after 1 second!"
    },
    {
      "type": "int",
      "value": 1000
    }
  ]
}
```

#### Task<T> を返すメソッド

```csharp
// C# コード例
public static class DataService
{
    public static async Task<string> FetchDataAsync(string url)
    {
        await Task.Delay(500); // Simulate network delay
        return $"Data from {url}: {DateTime.Now}";
    }
    
    public static async Task<int> CalculateAsync(int a, int b)
    {
        await Task.Delay(200);
        return a + b;
    }
}
```

MCP経由での呼び出し：

```json
{
  "typeName": "DataService",
  "methodName": "FetchDataAsync",
  "parameters": [
    {
      "type": "string",
      "value": "https://api.example.com/data"
    }
  ]
}
```

レスポンス例：

```
Successfully invoked async method DataService.FetchDataAsync

Return value: "Data from https://api.example.com/data: 2024-01-15 10:30:45"
Return type: System.Threading.Tasks.Task`1[System.String]
```

#### UniTask のサポート

UniTaskを使用している場合も同様に呼び出せます：

```csharp
// C# コード例
using Cysharp.Threading.Tasks;

public static class UniTaskOperations
{
    public static async UniTask<float> CalculateProgressAsync(int steps)
    {
        float progress = 0f;
        for (int i = 0; i < steps; i++)
        {
            await UniTask.Delay(100);
            progress = (float)(i + 1) / steps;
        }
        return progress;
    }
}
```

### 非同期メソッドの注意点

1. **自動的な待機**: ツールは自動的に非同期メソッドの完了を待機します
2. **戻り値の取得**: `Task<T>`や`UniTask<T>`の場合、完了後に結果が返されます
3. **エラーハンドリング**: 非同期メソッド内で発生した例外も適切にキャッチされます

## 高度な使用例

### カスタムクラスのstaticメソッド呼び出し

```csharp
// C# コード例
namespace MyGame.Utils
{
    public static class GameManager
    {
        public static void StartNewGame(string playerName, int difficulty, bool tutorialEnabled)
        {
            // ゲーム開始処理
        }
    }
}
```

MCP経由での呼び出し：

```json
{
  "typeName": "MyGame.Utils.GameManager",
  "methodName": "StartNewGame",
  "parameters": [
    {
      "type": "string",
      "value": "Player1"
    },
    {
      "type": "int",
      "value": 2
    },
    {
      "type": "bool",
      "value": true
    }
  ]
}
```

### 戻り値の取得

メソッドが値を返す場合、レスポンスに含まれます：

```csharp
// C# コード
public static class Calculator
{
    public static float Add(float a, float b)
    {
        return a + b;
    }
}
```

呼び出し：

```json
{
  "typeName": "Calculator",
  "methodName": "Add",
  "parameters": [
    {
      "type": "float",
      "value": 10.5
    },
    {
      "type": "float",
      "value": 20.3
    }
  ]
}
```

レスポンス例：

```
Successfully invoked Calculator.Add

Return value: 30.8
Return type: System.Single
```

## エラーハンドリング

### よくあるエラー

1. **型が見つからない**
   - 完全修飾型名を使用してください（namespace.className）
   - アセンブリがロードされていることを確認してください

2. **メソッドが見つからない**
   - メソッド名のスペルを確認してください
   - メソッドがstaticであることを確認してください
   - パラメータの数と型が一致していることを確認してください

3. **パラメータ変換エラー**
   - サポートされている型を使用してください
   - JSON形式が正しいことを確認してください

## セキュリティに関する注意

このツールは非常に強力で、任意のstaticメソッドを実行できます。以下の点に注意してください：

1. **信頼できるユーザーのみに使用を許可する**
2. **危険なメソッドの実行を避ける**（ファイル削除、システム設定変更など）
3. **本番環境では使用を制限することを検討する**
4. **非同期メソッドは完了まで実行が継続される**ため、長時間実行されるメソッドには注意

## トラブルシューティング

### メソッドが見つからない場合

1. 型名が正しいか確認（大文字小文字も含めて）
2. メソッドがpublicかつstaticであることを確認
3. パラメータの型と数が正確に一致しているか確認

### パラメータエラーの場合

1. JSON形式が正しいか確認
2. 型名が正しいか確認（`int`と`Int32`は同じ）
3. Unity型の場合、必要なフィールドがすべて含まれているか確認

## まとめ

InvokeStaticMethodToolを使用することで：

- Unity内のほぼすべてのstatic APIにアクセス可能
- 同期・非同期両方のメソッドをサポート
- 複雑なパラメータ型にも対応
- 戻り値の取得も可能

これにより、Unity Editorの機能を最大限に活用した柔軟な自動化が実現できます。

