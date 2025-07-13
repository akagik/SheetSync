# SheetSync 更新機能 設計補足資料

## 0. 本資料の目的

この補足資料は、メイン設計書（DESIGN_SheetSync_Update_Feature.md）の詳細実装と、実際の開発時に必要となる技術的詳細を提供します。

### 対象となる機能
- Google Spreadsheetsの特定行を検索・更新する機能
- 例：HumanMasterテーブルでhumanId=1のnameを"Taro"から"Tanaka"に変更

## 1. コンフリクト解決の詳細設計

### 1.1 楽観的ロックの実装

```csharp
public class OptimisticLockingStrategy
{
    // 各行にバージョン番号またはタイムスタンプを持たせる
    public class VersionedRow<T> where T : class
    {
        public T Data { get; set; }
        public string ETag { get; set; }  // Google Sheets APIのETag利用
        public DateTime LastModified { get; set; }
    }
    
    public async Task<UpdateResult> UpdateWithConflictDetection(
        VersionedRow<T> original,
        T updated)
    {
        // 現在のバージョンを取得
        var current = await GetCurrentVersion(original.RowId);
        
        if (current.ETag != original.ETag)
        {
            // コンフリクト検出
            var resolution = await ResolveConflict(original, current, updated);
            
            switch (resolution.Strategy)
            {
                case ConflictResolutionStrategy.Merge:
                    return await UpdateAsync(resolution.MergedData);
                    
                case ConflictResolutionStrategy.Overwrite:
                    return await ForceUpdateAsync(updated);
                    
                case ConflictResolutionStrategy.Abort:
                    throw new ConflictException(
                        "更新がキャンセルされました：他のユーザーによる変更があります");
            }
        }
        
        return await UpdateAsync(updated);
    }
}
```

### 1.2 3-Way マージアルゴリズム

```csharp
public class ThreeWayMerger<T> where T : class
{
    public MergeResult<T> Merge(
        T baseVersion,    // 共通の祖先
        T localVersion,   // ローカルの変更
        T remoteVersion)  // リモートの変更
    {
        var result = new MergeResult<T>();
        var properties = typeof(T).GetProperties();
        
        foreach (var prop in properties)
        {
            var baseValue = prop.GetValue(baseVersion);
            var localValue = prop.GetValue(localVersion);
            var remoteValue = prop.GetValue(remoteVersion);
            
            if (Equals(localValue, remoteValue))
            {
                // 両方同じ変更 or 変更なし
                result.SetProperty(prop.Name, localValue);
            }
            else if (Equals(baseValue, localValue))
            {
                // ローカル未変更、リモート変更
                result.SetProperty(prop.Name, remoteValue);
            }
            else if (Equals(baseValue, remoteValue))
            {
                // ローカル変更、リモート未変更
                result.SetProperty(prop.Name, localValue);
            }
            else
            {
                // 両方が異なる変更 = コンフリクト
                result.AddConflict(prop.Name, localValue, remoteValue);
            }
        }
        
        return result;
    }
}
```

## 2. スキーマバージョニングとマイグレーション

### 2.1 スキーマバージョン管理

```csharp
[SchemaVersion(2)]
public class HumanMasterV2 : HumanMaster
{
    // V1のフィールドは継承
    
    // V2で追加されたフィールド
    public string email { get; set; }
    
    // V2で型が変更されたフィールド
    public new DateTime birthDate { get; set; } // V1では int age
}

public interface ISchemaUpgrader<TFrom, TTo>
{
    TTo Upgrade(TFrom source);
    bool CanUpgrade(int fromVersion, int toVersion);
}

public class HumanMasterV1ToV2Upgrader : ISchemaUpgrader<HumanMaster, HumanMasterV2>
{
    public HumanMasterV2 Upgrade(HumanMaster source)
    {
        return new HumanMasterV2
        {
            humanId = source.humanId,
            name = source.name,
            birthDate = CalculateBirthDateFromAge(source.age),
            email = "", // デフォルト値
            companies = source.companies
        };
    }
}
```

### 2.2 動的スキーマ検出

```csharp
public class SchemaAnalyzer
{
    public async Task<SchemaInfo> AnalyzeSheetSchema(
        string sheetId,
        ConvertSetting setting)
    {
        // ヘッダー行を解析
        var headers = await GetHeadersAsync(sheetId);
        var typeRow = await GetTypeRowAsync(sheetId);
        
        var schema = new SchemaInfo
        {
            Version = DetectSchemaVersion(headers),
            Fields = new List<FieldInfo>()
        };
        
        for (int i = 0; i < headers.Count; i++)
        {
            schema.Fields.Add(new FieldInfo
            {
                Name = headers[i],
                Type = ParseType(typeRow[i]),
                Index = i,
                IsRequired = !IsOptionalField(headers[i])
            });
        }
        
        return schema;
    }
}
```

## 3. 高度なエラーハンドリング

### 3.1 エラー分類と自動リカバリー

```csharp
public enum ErrorCategory
{
    Transient,      // 一時的（リトライ可能）
    RateLimit,      // レート制限（待機後リトライ）
    Permission,     // 権限不足（リトライ不可）
    DataIntegrity,  // データ整合性（要確認）
    Network,        // ネットワーク（リトライ可能）
    Unknown         // 不明（要調査）
}

public class SmartErrorHandler
{
    private readonly Dictionary<Type, ErrorCategory> _errorClassification = new()
    {
        { typeof(GoogleApiException), ErrorCategory.Transient },
        { typeof(RateLimitExceededException), ErrorCategory.RateLimit },
        { typeof(UnauthorizedException), ErrorCategory.Permission },
        { typeof(DataValidationException), ErrorCategory.DataIntegrity },
        { typeof(HttpRequestException), ErrorCategory.Network }
    };
    
    public async Task<T> ExecuteWithRecovery<T>(
        Func<Task<T>> operation,
        RecoveryOptions options)
    {
        int attempt = 0;
        List<Exception> errors = new();
        
        while (attempt < options.MaxRetries)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex)
            {
                errors.Add(ex);
                var category = ClassifyError(ex);
                
                if (!ShouldRetry(category, attempt, options))
                {
                    throw new AggregateException(
                        $"操作が失敗しました（{errors.Count}回試行）", 
                        errors);
                }
                
                var delay = CalculateDelay(category, attempt);
                await Task.Delay(delay);
                
                attempt++;
            }
        }
        
        throw new MaxRetriesExceededException(errors);
    }
}
```

## 4. パフォーマンスメトリクスとSLA

### 4.1 パフォーマンス目標

| 操作 | 目標時間 | 最大許容時間 | 備考 |
|------|----------|--------------|------|
| 単一行検索 | 100ms | 500ms | キャッシュヒット時 |
| 単一行更新 | 200ms | 1s | API呼び出し含む |
| 100行バッチ更新 | 2s | 5s | 並列処理使用 |
| 1000行バッチ更新 | 10s | 30s | 分割バッチ処理 |
| 差分計算（1000行） | 50ms | 200ms | メモリ上の処理 |

### 4.2 パフォーマンス監視

```csharp
public class PerformanceMonitor
{
    private readonly IMetricsCollector _metrics;
    
    public async Task<T> MeasureAsync<T>(
        string operationName,
        Func<Task<T>> operation)
    {
        using var timer = _metrics.StartTimer(operationName);
        
        try
        {
            var result = await operation();
            timer.SetStatus("success");
            return result;
        }
        catch (Exception ex)
        {
            timer.SetStatus("failure");
            timer.SetTag("error_type", ex.GetType().Name);
            throw;
        }
    }
    
    public PerformanceReport GenerateReport(TimeSpan period)
    {
        var stats = _metrics.GetStatistics(period);
        
        return new PerformanceReport
        {
            AverageResponseTime = stats.Average(s => s.Duration),
            P95ResponseTime = stats.Percentile(95),
            P99ResponseTime = stats.Percentile(99),
            ErrorRate = stats.ErrorCount / (double)stats.TotalCount,
            Throughput = stats.TotalCount / period.TotalSeconds
        };
    }
}
```

## 5. オフライン対応とEventual Consistency

### 5.1 ローカル変更キュー

```csharp
public class OfflineChangeQueue
{
    private readonly ILocalStorage _storage;
    private readonly Queue<PendingChange> _changes = new();
    
    public void EnqueueChange(UpdateRequest request)
    {
        var change = new PendingChange
        {
            Id = Guid.NewGuid(),
            Request = request,
            Timestamp = DateTime.UtcNow,
            Status = ChangeStatus.Pending
        };
        
        _changes.Enqueue(change);
        _storage.SaveQueue(_changes);
    }
    
    public async Task SyncWithRetry()
    {
        while (_changes.TryPeek(out var change))
        {
            try
            {
                await ApplyChange(change);
                _changes.Dequeue();
                change.Status = ChangeStatus.Applied;
            }
            catch (ConflictException ex)
            {
                // コンフリクト解決UIを表示
                var resolution = await ShowConflictResolutionDialog(ex);
                if (resolution.Action == ConflictAction.Retry)
                {
                    change.Request = resolution.ModifiedRequest;
                }
                else
                {
                    _changes.Dequeue();
                    change.Status = ChangeStatus.Rejected;
                }
            }
            catch (Exception ex)
            {
                // 次回の同期で再試行
                break;
            }
        }
    }
}
```

## 6. セキュリティ詳細設計

### 6.1 フィールドレベルセキュリティ

```csharp
[AttributeUsage(AttributeTargets.Property)]
public class SecureFieldAttribute : Attribute
{
    public Permission RequiredPermission { get; set; }
    public bool AllowAnonymousRead { get; set; }
    public bool EnableAudit { get; set; }
}

public class HumanMasterSecure
{
    public int humanId { get; set; }
    
    [SecureField(RequiredPermission = Permission.ReadPII)]
    public string name { get; set; }
    
    [SecureField(
        RequiredPermission = Permission.UpdateSensitive,
        EnableAudit = true)]
    public int age { get; set; }
    
    [SecureField(
        RequiredPermission = Permission.Admin,
        AllowAnonymousRead = false)]
    public List<CompanyMaster> companies { get; set; }
}
```

### 6.2 監査ログの詳細

```csharp
public class DetailedAuditLogger
{
    public async Task LogFieldChange<T>(
        FieldChangeEvent<T> change,
        IUserContext user)
    {
        var entry = new AuditLogEntry
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            User = new UserInfo
            {
                Id = user.UserId,
                Name = user.UserName,
                IpAddress = user.IpAddress,
                SessionId = user.SessionId
            },
            Change = new ChangeInfo
            {
                EntityType = typeof(T).FullName,
                EntityId = change.EntityId,
                FieldName = change.FieldName,
                OldValue = SerializeValue(change.OldValue),
                NewValue = SerializeValue(change.NewValue),
                ChangeType = DetermineChangeType(change)
            },
            Context = new ContextInfo
            {
                Application = "SheetSync",
                Version = Assembly.GetExecutingAssembly().GetName().Version,
                Environment = EditorUserBuildSettings.activeBuildTarget.ToString()
            }
        };
        
        // 暗号化して保存
        var encrypted = await _encryption.EncryptAsync(entry);
        await _repository.SaveAuditLogAsync(encrypted);
    }
}
```

## 7. 実装上の注意点とベストプラクティス

### 7.1 メモリ効率

- 大規模データは`IAsyncEnumerable`でストリーム処理
- 不要なオブジェクトは即座に破棄（`using`）
- 弱参照（`WeakReference`）の活用

### 7.2 Unity固有の考慮事項

- メインスレッドでのUI更新（`EditorApplication.delayCall`）
- エディター再生時のクリーンアップ（`EditorApplication.playModeStateChanged`）
- ScriptableObjectの適切な管理

### 7.3 テスタビリティ

- すべての外部依存をインターフェース化
- 時刻はIDateTimeProviderで抽象化
- ランダム性はIRandomProviderで制御

## 8. 今後の発展可能性

1. **リアルタイム同期**
   - WebSocketによる変更通知
   - Operational Transformationの実装

2. **AI支援機能**
   - 変更パターンの学習
   - 異常検知

3. **ビジュアルクエリビルダー**
   - ドラッグ&ドロップでクエリ作成
   - プレビュー機能

4. **他システムとの連携**
   - Slack通知
   - Git連携（変更の自動コミット）

## 9. 実装開始のためのクイックスタート

### 9.1 最小実装（MVP）の手順

**Step 1: 基本的なクエリビルダー**
```csharp
// 最初は簡単な実装から
public class SimpleUpdateQuery<T>
{
    public string FieldName { get; set; }
    public object SearchValue { get; set; }
    public string UpdateFieldName { get; set; }
    public object UpdateValue { get; set; }
}

// 使用例
var query = new SimpleUpdateQuery<HumanMaster>
{
    FieldName = "humanId",
    SearchValue = 1,
    UpdateFieldName = "name", 
    UpdateValue = "Tanaka"
};
```

**Step 2: Google Sheets API統合**
```csharp
public async Task<bool> UpdateSingleRow(SimpleUpdateQuery<T> query)
{
    // 1. 該当行を検索
    var rowIndex = await FindRowIndex(query.FieldName, query.SearchValue);
    
    // 2. 更新対象のセル位置を計算
    var cellAddress = CalculateCellAddress(rowIndex, query.UpdateFieldName);
    
    // 3. 値を更新
    return await UpdateCell(cellAddress, query.UpdateValue);
}
```

**Step 3: 差分確認UI**
```csharp
if (EditorUtility.DisplayDialog(
    "更新の確認",
    $"以下の変更を適用しますか？\n\n" +
    $"Row {rowIndex} (humanId={searchValue}):\n" +
    $"  name: \"{oldValue}\" → \"{newValue}\"",
    "更新", "キャンセル"))
{
    // 更新実行
}
```

### 9.2 段階的な機能追加

1. **Phase 1（1週間）**: 基本的な単一フィールド更新
2. **Phase 2（1週間）**: Expression Tree対応
3. **Phase 3（2週間）**: バッチ処理とUI改善
4. **Phase 4（1週間）**: エラーハンドリングとテスト

### 9.3 必要なファイル構成

```
Packages/SheetSync/Editor/SheetSync/
├── Services/
│   └── Update/
│       ├── SheetUpdateService.cs        # メインサービス
│       ├── QueryBuilder.cs              # クエリビルダー
│       └── UpdateResult.cs              # 結果クラス
├── UI/
│   └── Windows/
│       └── SheetUpdateWindow.cs         # 更新UI
└── Tests/
    └── Update/
        └── SheetUpdateServiceTests.cs   # テスト
```