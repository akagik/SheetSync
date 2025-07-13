# SheetSync 更新機能 設計書 v2.0

## 1. 設計レビューと問題点の分析

### 1.1 初期設計の問題点

#### a) 型安全性の欠如
- フィールド名を文字列で指定 → コンパイル時エラー検出不可
- リフレクション依存 → パフォーマンス低下とランタイムエラーのリスク
- 型変換の安全性が保証されない

#### b) スケーラビリティの問題
- 全データ取得方式 → 大規模データで破綻
- メモリ効率が悪い
- ネットワーク帯域の無駄遣い

#### c) データ整合性リスク
- 楽観的ロックなし → 同時編集で上書きリスク
- トランザクション未対応 → 部分的失敗時の不整合
- ロールバック機能なし

#### d) 保守性・拡張性の課題
- マスターテーブルごとに実装が必要
- 検索ロジックが固定的
- テストが困難

## 2. 改訂版アーキテクチャ設計

### 2.1 コア設計思想

```
1. 型安全性ファースト - Expression Treeベースのクエリシステム
2. 差分同期 - 必要最小限のデータ転送
3. イベントドリブン - 拡張可能なフック機構
4. ジェネリック設計 - 再利用可能なコンポーネント
```

### 2.2 レイヤードアーキテクチャ

```
┌─────────────────────────────────────────────────┐
│             Presentation Layer                   │
│  ┌─────────────────┐  ┌───────────────────┐    │
│  │ UpdateWindow<T> │  │ DiffViewerDialog  │    │
│  └─────────────────┘  └───────────────────┘    │
├─────────────────────────────────────────────────┤
│             Application Layer                    │
│  ┌─────────────────┐  ┌───────────────────┐    │
│  │ UpdateWorkflow  │  │ ValidationService │    │
│  └─────────────────┘  └───────────────────┘    │
├─────────────────────────────────────────────────┤
│               Domain Layer                       │
│  ┌─────────────────┐  ┌───────────────────┐    │
│  │  QueryBuilder<T> │  │ ChangeTracker<T>  │    │
│  └─────────────────┘  └───────────────────┘    │
├─────────────────────────────────────────────────┤
│           Infrastructure Layer                   │
│  ┌─────────────────┐  ┌───────────────────┐    │
│  │SheetRepository  │  │  CacheManager     │    │
│  └─────────────────┘  └───────────────────┘    │
└─────────────────────────────────────────────────┘
```

## 3. 詳細設計

### 3.1 型安全なクエリシステム

```csharp
// 使用例
var query = QueryBuilder<HumanMaster>
    .Where(h => h.humanId == 1)
    .Update(h => h.name, "Tanaka")
    .Build();

// 複数条件・複数更新
var batchQuery = QueryBuilder<HumanMaster>
    .Where(h => h.age > 30 && h.name.StartsWith("K"))
    .Update(h => h.age, h => h.age + 1)
    .Update(h => h.name, h => h.name.ToUpper())
    .Build();
```

#### 実装詳細

```csharp
public class QueryBuilder<T> where T : class
{
    private readonly List<Expression<Func<T, bool>>> _predicates;
    private readonly List<UpdateExpression<T>> _updates;
    
    public QueryBuilder<T> Where(Expression<Func<T, bool>> predicate)
    {
        _predicates.Add(predicate);
        return this;
    }
    
    public QueryBuilder<T> Update<TProperty>(
        Expression<Func<T, TProperty>> propertySelector,
        TProperty newValue)
    {
        var update = new UpdateExpression<T>
        {
            PropertyPath = GetPropertyPath(propertySelector),
            NewValue = newValue,
            PropertyType = typeof(TProperty)
        };
        _updates.Add(update);
        return this;
    }
}
```

### 3.2 差分追跡システム

```csharp
public class ChangeTracker<T> where T : class
{
    private readonly Dictionary<string, RowSnapshot> _originalSnapshots;
    private readonly Dictionary<string, RowSnapshot> _currentSnapshots;
    
    public ChangeSet<T> CalculateChanges()
    {
        var changes = new ChangeSet<T>();
        
        foreach (var kvp in _currentSnapshots)
        {
            var rowId = kvp.Key;
            var current = kvp.Value;
            
            if (_originalSnapshots.TryGetValue(rowId, out var original))
            {
                var diff = CompareSnapshots(original, current);
                if (diff.HasChanges)
                {
                    changes.AddModification(rowId, diff);
                }
            }
        }
        
        return changes;
    }
    
    public void BeginTracking(IEnumerable<T> entities)
    {
        foreach (var entity in entities)
        {
            var snapshot = CreateSnapshot(entity);
            _originalSnapshots[snapshot.RowId] = snapshot;
            _currentSnapshots[snapshot.RowId] = snapshot.Clone();
        }
    }
}
```

### 3.3 バッチ更新の最適化

```csharp
public class BatchUpdateOptimizer
{
    private const int MaxBatchSize = 1000;
    private const int MaxConcurrentRequests = 5;
    
    public async Task<UpdateResult> ExecuteBatchUpdateAsync(
        ChangeSet changeSet,
        ISheetRepository repository)
    {
        // 変更を最適なバッチサイズに分割
        var batches = changeSet.Changes
            .Chunk(MaxBatchSize)
            .Select(chunk => new UpdateBatch(chunk))
            .ToList();
        
        // 並列実行（制限付き）
        using var semaphore = new SemaphoreSlim(MaxConcurrentRequests);
        var tasks = batches.Select(async batch =>
        {
            await semaphore.WaitAsync();
            try
            {
                return await repository.UpdateBatchAsync(batch);
            }
            finally
            {
                semaphore.Release();
            }
        });
        
        var results = await Task.WhenAll(tasks);
        return MergeResults(results);
    }
}
```

### 3.4 イベントシステムとフック

```csharp
public interface IUpdateHook<T> where T : class
{
    Task<ValidationResult> ValidateBeforeUpdateAsync(
        T original, T updated, UpdateContext context);
    
    Task OnAfterUpdateAsync(
        T original, T updated, UpdateContext context);
}

// 使用例：監査ログ
public class AuditLogHook<T> : IUpdateHook<T> where T : class
{
    public async Task OnAfterUpdateAsync(
        T original, T updated, UpdateContext context)
    {
        var auditEntry = new AuditLogEntry
        {
            Timestamp = DateTime.UtcNow,
            User = context.User,
            EntityType = typeof(T).Name,
            Changes = JsonSerializer.Serialize(new
            {
                Before = original,
                After = updated
            })
        };
        
        await _auditRepository.SaveAsync(auditEntry);
    }
}
```

### 3.5 エラーハンドリングとリトライ

```csharp
public class ResilientSheetRepository : ISheetRepository
{
    private readonly IRetryPolicy _retryPolicy;
    
    public async Task<UpdateResult> UpdateAsync(UpdateRequest request)
    {
        return await _retryPolicy.ExecuteAsync(async () =>
        {
            try
            {
                return await _innerRepository.UpdateAsync(request);
            }
            catch (GoogleApiException ex) when (ex.Error.Code == 429)
            {
                // Rate limit - exponential backoff
                throw new RetriableException(ex);
            }
            catch (GoogleApiException ex) when (ex.Error.Code == 503)
            {
                // Service unavailable - retry
                throw new RetriableException(ex);
            }
        });
    }
}
```

## 4. UI/UX設計

### 4.1 プログレッシブUI

```csharp
public class UpdateProgressUI : IProgress<UpdateProgress>
{
    private readonly EditorWindow _window;
    private readonly CancellationTokenSource _cts;
    
    public void Report(UpdateProgress value)
    {
        EditorUtility.DisplayProgressBar(
            $"更新中... ({value.ProcessedRows}/{value.TotalRows})",
            value.CurrentOperation,
            value.PercentComplete);
            
        if (value.HasErrors)
        {
            ShowErrorDialog(value.Errors);
        }
    }
}
```

### 4.2 差分ビューアー

```
┌─────────────────────────────────────────────┐
│         変更内容の確認                       │
├─────────────────────────────────────────────┤
│ ▼ Row 3 (humanId=1)                        │
│   name: "Taro" → "Tanaka"                  │
│   [元に戻す]                                │
│                                             │
│ ▼ Row 5 (humanId=2)                        │
│   age: 45 → 46                             │
│   name: "Mami" → "MAMI"                    │
│   [元に戻す]                                │
├─────────────────────────────────────────────┤
│ 総変更数: 3フィールド (2行)                 │
│ [すべて元に戻す] [キャンセル] [適用]        │
└─────────────────────────────────────────────┘
```

## 5. セキュリティと権限管理

### 5.1 権限チェック

```csharp
[RequirePermission(Permission.UpdateSheet)]
public class SecureUpdateService : IUpdateService
{
    private readonly IPermissionService _permissions;
    
    public async Task<UpdateResult> UpdateAsync(
        UpdateRequest request,
        IUserContext user)
    {
        // フィールドレベルの権限チェック
        foreach (var field in request.UpdateFields)
        {
            if (!await _permissions.CanUpdateFieldAsync(
                user, request.EntityType, field.Name))
            {
                throw new UnauthorizedException(
                    $"フィールド '{field.Name}' の更新権限がありません");
            }
        }
        
        return await _innerService.UpdateAsync(request, user);
    }
}
```

## 6. テスト戦略

### 6.1 単体テスト

```csharp
[TestFixture]
public class QueryBuilderTests
{
    [Test]
    public void Where_WithValidExpression_BuildsCorrectQuery()
    {
        // Arrange & Act
        var query = QueryBuilder<HumanMaster>
            .Where(h => h.humanId == 1)
            .Update(h => h.name, "Tanaka")
            .Build();
        
        // Assert
        Assert.That(query.Predicates.Count, Is.EqualTo(1));
        Assert.That(query.Updates.Count, Is.EqualTo(1));
        Assert.That(query.Updates[0].PropertyPath, Is.EqualTo("name"));
        Assert.That(query.Updates[0].NewValue, Is.EqualTo("Tanaka"));
    }
    
    [Test]
    public void Update_WithInvalidType_ThrowsCompileTimeError()
    {
        // This should not compile
        // QueryBuilder<HumanMaster>
        //     .Update(h => h.age, "not a number");
    }
}
```

### 6.2 統合テスト

```csharp
[TestFixture]
public class SheetUpdateIntegrationTests
{
    private MockSheetRepository _mockRepo;
    private UpdateService _service;
    
    [Test]
    public async Task UpdateMultipleRows_WithBatching_SucceedsEfficiently()
    {
        // Arrange
        var testData = GenerateTestData(5000);
        _mockRepo.SetupData(testData);
        
        var query = QueryBuilder<HumanMaster>
            .Where(h => h.age > 30)
            .Update(h => h.age, h => h.age + 1)
            .Build();
        
        // Act
        var result = await _service.UpdateAsync(query);
        
        // Assert
        Assert.That(result.UpdatedRows, Is.EqualTo(2500));
        Assert.That(_mockRepo.BatchRequestCount, Is.LessThanOrEqualTo(3));
    }
}
```

## 7. パフォーマンス最適化

### 7.1 キャッシング戦略

```csharp
public class CachedSheetRepository : ISheetRepository
{
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);
    
    public async Task<SheetData> GetDataAsync(string sheetId, string range)
    {
        var cacheKey = $"{sheetId}:{range}";
        
        if (_cache.TryGetValue<SheetData>(cacheKey, out var cached))
        {
            return cached;
        }
        
        var data = await _innerRepository.GetDataAsync(sheetId, range);
        
        _cache.Set(cacheKey, data, _cacheExpiration);
        
        return data;
    }
}
```

### 7.2 非同期ストリーミング

```csharp
public async IAsyncEnumerable<T> StreamLargeDataAsync<T>(
    Query query,
    [EnumeratorCancellation] CancellationToken ct = default)
{
    const int PageSize = 100;
    int offset = 0;
    
    while (!ct.IsCancellationRequested)
    {
        var page = await GetPageAsync(query, offset, PageSize);
        
        if (!page.Any())
            yield break;
            
        foreach (var item in page)
        {
            yield return item;
        }
        
        offset += PageSize;
    }
}
```

## 8. 実装ロードマップ

### Phase 1: 基盤構築（2週間）
- [ ] 型安全クエリビルダーの実装
- [ ] 基本的な単一更新機能
- [ ] 単体テストフレームワーク

### Phase 2: 高度な機能（3週間）
- [ ] バッチ更新の最適化
- [ ] 差分追跡システム
- [ ] プログレスUI

### Phase 3: エンタープライズ機能（2週間）
- [ ] 権限管理システム
- [ ] 監査ログ
- [ ] エラーリカバリー

### Phase 4: パフォーマンス最適化（1週間）
- [ ] キャッシング
- [ ] 非同期ストリーミング
- [ ] 負荷テスト

## 9. リスクと対策

| リスク | 影響度 | 発生確率 | 対策 |
|--------|--------|----------|------|
| Google API制限 | 高 | 中 | バッチ処理、レート制限、キャッシング |
| 大規模データでのメモリ不足 | 高 | 低 | ストリーミング処理、ページング |
| 同時編集による競合 | 中 | 高 | 楽観的ロック、差分マージ |
| 型定義の変更 | 中 | 中 | スキーマバージョニング |

## 10. まとめ

この改訂版設計では、初期設計の問題点を解決し、以下を実現します：

1. **型安全性**: Expression Treeによるコンパイル時チェック
2. **スケーラビリティ**: 差分同期とストリーミング処理
3. **保守性**: ジェネリック設計とイベントドリブンアーキテクチャ
4. **信頼性**: トランザクション、リトライ、監査ログ
5. **パフォーマンス**: キャッシング、バッチ最適化、非同期処理

この設計により、エンタープライズレベルの要求にも対応できる堅牢なシステムを構築できます。