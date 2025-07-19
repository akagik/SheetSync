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

