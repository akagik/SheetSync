# Google Sheets API 直接インポート機能 - リファクタリング計画書 第2版

## 1. 新たな要件と懸念事項への対応

### 1.1 主要な懸念事項
1. **パフォーマンス問題**: シート全体を一気に取得するため処理が重い
2. **不要なデータ取得**: 型名がない列や空行も取得している
3. **差分更新の欠如**: 毎回全データを処理している
4. **変更検知の不在**: どこが変更されたか分からない

### 1.2 解決方針
- **スマート取得**: 必要な範囲のみを取得
- **差分同期**: 変更された部分のみを更新
- **変更追跡**: スプレッドシートの変更履歴を活用

## 2. 提案する新アーキテクチャ

### 2.1 システム全体図
```
Google Sheets API
    ↓
範囲指定取得 & メタデータ取得
    ↓
差分検出エンジン
    ↓
部分的な CsvData 更新
    ↓
アセット生成（変更分のみ）
```

### 2.2 コア機能の設計

#### 2.2.1 スマートレンジ検出システム
```csharp
public class SheetRangeOptimizer
{
    /// <summary>
    /// 有効なデータ範囲を自動検出
    /// </summary>
    public class DataRange
    {
        public int StartRow { get; set; } = 1;  // ヘッダー行
        public int EndRow { get; set; }
        public string StartColumn { get; set; } = "A";
        public string EndColumn { get; set; }
        
        // 型定義がある列のみを含む範囲
        public List<int> ValidColumnIndices { get; set; }
    }
    
    /// <summary>
    /// 2行目の型定義を基に有効な列を検出
    /// </summary>
    public async Task<DataRange> DetectValidRangeAsync(string spreadsheetId, string sheetName)
    {
        // 1. まず2行目（型定義行）のみを取得
        var typeRowRange = $"{sheetName}!A2:ZZ2";
        var typeRowData = await GetRangeDataAsync(spreadsheetId, typeRowRange);
        
        // 2. 型定義がある列を特定
        var validColumns = IdentifyValidColumns(typeRowData);
        
        // 3. 最終行を効率的に検出（バイナリサーチ）
        var lastRow = await DetectLastRowAsync(spreadsheetId, sheetName, validColumns);
        
        return new DataRange
        {
            EndRow = lastRow,
            ValidColumnIndices = validColumns,
            EndColumn = GetColumnLetter(validColumns.Last())
        };
    }
    
    /// <summary>
    /// バイナリサーチで最終行を高速検出
    /// </summary>
    private async Task<int> DetectLastRowAsync(string spreadsheetId, string sheetName, List<int> validColumns)
    {
        int low = 3;  // データ開始行
        int high = 10000;  // 初期推定値
        
        // まず大まかな範囲を特定
        while (await HasDataInRowAsync(spreadsheetId, sheetName, high, validColumns))
        {
            low = high;
            high *= 2;
        }
        
        // バイナリサーチで正確な位置を特定
        while (low < high)
        {
            int mid = (low + high + 1) / 2;
            if (await HasDataInRowAsync(spreadsheetId, sheetName, mid, validColumns))
            {
                low = mid;
            }
            else
            {
                high = mid - 1;
            }
        }
        
        return low;
    }
}
```

#### 2.2.2 差分検出・更新システム
```csharp
public class DifferentialSyncEngine
{
    private Dictionary<string, SheetSnapshot> _snapshots = new();
    
    public class SheetSnapshot
    {
        public string SpreadsheetId { get; set; }
        public string SheetName { get; set; }
        public DateTime LastSyncTime { get; set; }
        public string LastRevisionId { get; set; }
        public Dictionary<string, string> CellChecksums { get; set; } // セルアドレス -> チェックサム
        public string OverallChecksum { get; set; }
    }
    
    public class SyncResult
    {
        public List<CellChange> Changes { get; set; }
        public List<int> AddedRows { get; set; }
        public List<int> DeletedRows { get; set; }
        public bool HasStructuralChanges { get; set; }  // 列構造の変更
    }
    
    /// <summary>
    /// Google Sheets API のリビジョン情報を使用した変更検知
    /// </summary>
    public async Task<bool> HasChangedSinceLastSyncAsync(string spreadsheetId)
    {
        // Google Sheets API v4 の revisionId を使用
        var metadata = await GetSpreadsheetMetadataAsync(spreadsheetId);
        var currentRevisionId = metadata.Properties.RevisionId;
        
        if (_snapshots.TryGetValue(spreadsheetId, out var snapshot))
        {
            return snapshot.LastRevisionId != currentRevisionId;
        }
        
        return true;  // 初回は常に変更ありとする
    }
    
    /// <summary>
    /// 差分のみを取得して更新
    /// </summary>
    public async Task<SyncResult> SyncDifferentialAsync(
        string spreadsheetId, 
        string sheetName,
        SheetRangeOptimizer.DataRange validRange)
    {
        var result = new SyncResult();
        
        // 1. 変更があるかチェック
        if (!await HasChangedSinceLastSyncAsync(spreadsheetId))
        {
            return result;  // 変更なし
        }
        
        // 2. バッチ取得で必要な範囲のみ取得
        var batchRequest = new BatchGetRequest
        {
            Ranges = GenerateOptimalRanges(validRange),
            MajorDimension = "ROWS"
        };
        
        var response = await BatchGetAsync(spreadsheetId, batchRequest);
        
        // 3. 差分を計算
        result = CalculateDifferences(_snapshots[spreadsheetId], response);
        
        // 4. スナップショットを更新
        UpdateSnapshot(spreadsheetId, sheetName, response);
        
        return result;
    }
    
    /// <summary>
    /// 最適な取得範囲を生成（必要な列のみ）
    /// </summary>
    private List<string> GenerateOptimalRanges(SheetRangeOptimizer.DataRange validRange)
    {
        var ranges = new List<string>();
        
        // 連続する列はまとめて取得
        var columnGroups = GroupConsecutiveColumns(validRange.ValidColumnIndices);
        
        foreach (var group in columnGroups)
        {
            var startCol = GetColumnLetter(group.First());
            var endCol = GetColumnLetter(group.Last());
            ranges.Add($"{startCol}1:{endCol}{validRange.EndRow}");
        }
        
        return ranges;
    }
}
```

#### 2.2.3 インクリメンタル更新システム
```csharp
public class IncrementalAssetUpdater
{
    /// <summary>
    /// 変更されたアセットのみを更新
    /// </summary>
    public async Task UpdateAssetsIncrementallyAsync(
        DifferentialSyncEngine.SyncResult syncResult,
        ConvertSetting setting)
    {
        // 1. 影響を受けるアセットを特定
        var affectedAssets = IdentifyAffectedAssets(syncResult, setting);
        
        // 2. 構造的変更がある場合はクラス再生成
        if (syncResult.HasStructuralChanges)
        {
            await RegenerateClassesAsync(setting);
            AssetDatabase.Refresh();
        }
        
        // 3. データ変更のみの場合は該当アセットのみ更新
        foreach (var assetInfo in affectedAssets)
        {
            UpdateSingleAsset(assetInfo, syncResult.Changes);
        }
        
        // 4. 新規行の場合は新規アセット作成
        foreach (var newRowIndex in syncResult.AddedRows)
        {
            CreateNewAsset(newRowIndex, setting);
        }
        
        // 5. 削除された行のアセットを処理
        foreach (var deletedRowIndex in syncResult.DeletedRows)
        {
            HandleDeletedAsset(deletedRowIndex, setting);
        }
    }
}
```

## 3. パフォーマンス最適化戦略

### 3.1 データ取得の最適化

#### 3.1.1 列フィルタリング
```csharp
public class ColumnFilter
{
    /// <summary>
    /// 型定義がある列のみを取得対象とする
    /// </summary>
    public List<string> GetRequiredColumns(IList<object> typeRow)
    {
        var columns = new List<string>();
        
        for (int i = 0; i < typeRow.Count; i++)
        {
            if (typeRow[i] != null && !string.IsNullOrWhiteSpace(typeRow[i].ToString()))
            {
                columns.Add(GetColumnLetter(i));
            }
        }
        
        return columns;
    }
}
```

#### 3.1.2 動的バッチサイズ
```csharp
public class AdaptiveBatchProcessor
{
    private const int MAX_BATCH_SIZE = 1000;
    private const int MIN_BATCH_SIZE = 100;
    
    /// <summary>
    /// ネットワーク状況に応じてバッチサイズを調整
    /// </summary>
    public async Task<List<T>> ProcessInAdaptiveBatchesAsync<T>(
        List<int> rowIndices,
        Func<List<int>, Task<List<T>>> processor)
    {
        var results = new List<T>();
        var currentBatchSize = MAX_BATCH_SIZE;
        
        for (int i = 0; i < rowIndices.Count; i += currentBatchSize)
        {
            var batch = rowIndices.Skip(i).Take(currentBatchSize).ToList();
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                var batchResult = await processor(batch);
                results.AddRange(batchResult);
                
                // 成功時は処理時間に基づいてバッチサイズを調整
                stopwatch.Stop();
                currentBatchSize = AdjustBatchSize(currentBatchSize, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                // エラー時はバッチサイズを縮小
                currentBatchSize = Math.Max(MIN_BATCH_SIZE, currentBatchSize / 2);
                i -= currentBatchSize;  // リトライ
            }
        }
        
        return results;
    }
}
```

### 3.2 キャッシング戦略

```csharp
public class SheetDataCache
{
    private readonly Dictionary<string, CacheEntry> _cache = new();
    
    public class CacheEntry
    {
        public object Data { get; set; }
        public DateTime CachedAt { get; set; }
        public string Checksum { get; set; }
        public TimeSpan TTL { get; set; }
    }
    
    /// <summary>
    /// セル単位のキャッシュで部分更新をサポート
    /// </summary>
    public void UpdatePartialCache(string sheetId, Dictionary<string, object> cellUpdates)
    {
        if (_cache.TryGetValue(sheetId, out var entry))
        {
            var sheetData = entry.Data as SheetData;
            foreach (var update in cellUpdates)
            {
                sheetData.UpdateCell(update.Key, update.Value);
            }
            entry.Checksum = CalculateChecksum(sheetData);
        }
    }
}
```

## 4. 変更検知機能の実装

### 4.1 Google Sheets 変更履歴の活用

```csharp
public class ChangeTracker
{
    /// <summary>
    /// Google Sheets の変更履歴APIを使用
    /// </summary>
    public async Task<List<SheetChange>> GetChangesSinceAsync(
        string spreadsheetId, 
        DateTime sinceTime)
    {
        // Google Drive API v3 の changes.list を使用
        var changes = await driveService.Changes.List()
            .SetFields("changes(file(id,name,modifiedTime))")
            .SetIncludeItemsFromAllDrives(true)
            .SetRestrictToMyDrive(false)
            .SetQ($"'{spreadsheetId}' in parents and modifiedTime > '{sinceTime:yyyy-MM-dd'T'HH:mm:ss}'")
            .ExecuteAsync();
        
        return MapToSheetChanges(changes);
    }
    
    /// <summary>
    /// セルレベルの変更追跡
    /// </summary>
    public class CellChangeDetector
    {
        private Dictionary<string, string> _previousChecksums = new();
        
        public List<CellChange> DetectCellChanges(
            Dictionary<string, object> currentData,
            Dictionary<string, object> previousData)
        {
            var changes = new List<CellChange>();
            
            // 追加・変更されたセル
            foreach (var cell in currentData)
            {
                if (!previousData.TryGetValue(cell.Key, out var oldValue) || 
                    !Equals(cell.Value, oldValue))
                {
                    changes.Add(new CellChange
                    {
                        CellAddress = cell.Key,
                        OldValue = oldValue,
                        NewValue = cell.Value,
                        ChangeType = oldValue == null ? ChangeType.Added : ChangeType.Modified
                    });
                }
            }
            
            // 削除されたセル
            foreach (var cell in previousData)
            {
                if (!currentData.ContainsKey(cell.Key))
                {
                    changes.Add(new CellChange
                    {
                        CellAddress = cell.Key,
                        OldValue = cell.Value,
                        NewValue = null,
                        ChangeType = ChangeType.Deleted
                    });
                }
            }
            
            return changes;
        }
    }
}
```

### 4.2 変更通知システム

```csharp
public class ChangeNotificationService
{
    public delegate void SheetChangedHandler(SheetChangeEvent e);
    public event SheetChangedHandler OnSheetChanged;
    
    /// <summary>
    /// 変更内容を開発者に通知
    /// </summary>
    public void NotifyChanges(DifferentialSyncEngine.SyncResult syncResult)
    {
        var changeEvent = new SheetChangeEvent
        {
            Timestamp = DateTime.Now,
            ChangedCells = syncResult.Changes.Select(c => c.CellAddress).ToList(),
            AddedRows = syncResult.AddedRows,
            DeletedRows = syncResult.DeletedRows,
            Summary = GenerateChangeSummary(syncResult)
        };
        
        OnSheetChanged?.Invoke(changeEvent);
        
        // Unity Editor のコンソールにも出力
        if (Application.isEditor)
        {
            Debug.Log($"[SheetSync] 変更検出: {changeEvent.Summary}");
        }
    }
}
```

## 5. 実装計画

### Phase 1: 基盤構築（2日）
1. `SheetRangeOptimizer` の実装
2. 列フィルタリング機能の実装
3. 基本的な範囲検出アルゴリズムの実装

### Phase 2: 差分検出システム（3日）
1. `DifferentialSyncEngine` の実装
2. スナップショット管理機能
3. Google Sheets API のリビジョン活用

### Phase 3: インクリメンタル更新（2日）
1. `IncrementalAssetUpdater` の実装
2. 部分的なアセット更新機能
3. 変更通知システム

### Phase 4: 最適化とキャッシング（2日）
1. アダプティブバッチ処理
2. キャッシングレイヤーの実装
3. パフォーマンステスト

### Phase 5: UI統合と仕上げ（1日）
1. エディターUIの更新
2. 進捗表示の実装
3. エラーハンドリングの強化

## 6. 期待される効果

### 6.1 パフォーマンス改善
- **初回読み込み**: 必要な列のみ取得により 40-60% 高速化
- **更新時**: 差分のみの処理により 80-90% 高速化
- **メモリ使用量**: 必要なデータのみ保持により 50% 削減

### 6.2 ユーザビリティ向上
- 変更箇所の可視化
- リアルタイムに近い同期体験
- 大規模スプレッドシートでも快適な動作

### 6.3 開発効率の向上
- 変更検知により必要な部分のみビルド
- デバッグ時の変更追跡が容易
- チーム開発での競合検出

## 7. 設定とオプション

```csharp
public class OptimizedSyncSettings
{
    // 基本設定
    public bool EnableDifferentialSync = true;
    public bool EnableColumnFiltering = true;
    public bool EnableChangeNotifications = true;
    
    // パフォーマンス設定
    public int MaxBatchSize = 1000;
    public int CacheTTLMinutes = 5;
    public bool EnableAdaptiveBatching = true;
    
    // 範囲検出設定
    public int MaxRowsToScan = 100000;
    public bool AutoDetectDataRange = true;
    public List<string> IgnoreColumns = new();  // 除外する列
    
    // 変更検知設定
    public bool TrackCellLevelChanges = false;  // セル単位の追跡（重い）
    public int ChangeHistoryRetentionDays = 7;
}
```

## 8. エラーハンドリングと復旧

```csharp
public class SyncErrorHandler
{
    /// <summary>
    /// 同期エラーからの自動復旧
    /// </summary>
    public async Task<bool> RecoverFromSyncErrorAsync(Exception error, ConvertSetting setting)
    {
        if (error is Google.GoogleApiException apiError)
        {
            switch (apiError.HttpStatusCode)
            {
                case HttpStatusCode.TooManyRequests:
                    // レート制限エラー: 指数バックオフでリトライ
                    return await RetryWithExponentialBackoffAsync();
                    
                case HttpStatusCode.Unauthorized:
                    // 認証エラー: トークンをリフレッシュ
                    return await RefreshAuthenticationAsync();
                    
                case HttpStatusCode.NotFound:
                    // シートが見つからない: フルスキャンで再検出
                    return await RedetectSheetStructureAsync(setting);
            }
        }
        
        // その他のエラーはフォールバック処理
        return await FallbackToFullSyncAsync(setting);
    }
}
```

## 9. まとめ

この第2版の計画では、以下の点を重点的に改善しました：

1. **効率的なデータ取得**: 必要な範囲・列のみを取得
2. **差分同期**: 変更部分のみを処理
3. **変更検知**: Google Sheets APIの機能を活用した変更追跡
4. **段階的な実装**: リスクを最小化しながら着実に改善

これにより、大規模なスプレッドシートでも高速で効率的な同期が可能になり、開発者の生産性が大幅に向上することが期待されます。