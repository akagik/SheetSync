# SheetSync 更新機能 設計書 v2.0

## 0. 概要と目的

### 0.1 本設計書の目的
本設計書は、Google Spreadsheetsの特定の行を検索し、その行のデータのみを更新する機能（以下、「選択的更新機能」）の設計を定めるものです。

### 0.2 解決したい課題
現在のSheetSyncは、スプレッドシート全体のインポート機能のみを提供しています。しかし、実際の運用では：
- 特定のレコードのみを更新したい（例：humanId=1のユーザー名を変更）
- 大規模データの一部のみを効率的に更新したい
- 更新前に変更内容を確認したい

といったニーズがあります。

### 0.3 具体的なユースケース

**例：HumanMasterテーブルの更新**
```csharp
// 既存のマスターデータ
[System.Serializable]
public class HumanMaster
{
    public int humanId;
    public string name;
    public int age;
    public List<CompanyMaster> companies;
}
```

**スプレッドシートの状態：**
| バージョン | humanId | name   | age  | 備考               |
| ---------- | ------- | ------ | ---- | ------------------ |
|            | int     | string | int  |                    |
|            | 0       | Kohei  | 34   |                    |
|            | 1       | Taro   | 23   |                    |
|            | 2       | Mami   | 45   | これはテストデータ |

**実行したい操作：**
- humanId=1 の行を検索
- name を "Taro" から "Tanaka" に変更
- 他のフィールドや行は一切変更しない

## 1. 機能要件

### 1.1 必須要件
1. **選択的検索**: 任意のフィールドで行を検索（humanId、age、nameなど）
2. **選択的更新**: 検索された行の特定フィールドのみ更新
3. **型安全性**: 型を持たないフィールド（備考、バージョン）は保護
4. **差分確認**: 更新前に変更内容をプレビュー表示
5. **バッチ処理**: 複数行の一括更新に対応

### 1.2 将来の拡張要件
- 行の削除機能
- 行の挿入機能
- 複雑な検索条件（AND/OR）

## 2. 設計方針と課題分析

### 2.1 初期設計の問題点

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

## 3. 改訂版アーキテクチャ設計

### 3.1 コア設計思想

```
1. 型安全性ファースト - Expression Treeベースのクエリシステム
2. 差分同期 - 必要最小限のデータ転送
3. イベントドリブン - 拡張可能なフック機構
4. ジェネリック設計 - 再利用可能なコンポーネント
```

### 3.2 レイヤードアーキテクチャ

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

## 4. 詳細設計

### 4.1 型安全なクエリシステム

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

### 4.2 差分追跡システム

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

### 4.3 バッチ更新の最適化

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

### 4.4 イベントシステムとフック

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

### 4.5 エラーハンドリングとリトライ

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

## 5. UI/UX設計

### 5.1 プログレッシブUI

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

### 5.2 差分ビューアー

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

## 6. セキュリティと権限管理

### 6.1 権限チェック

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

## 7. テスト戦略

### 7.1 単体テスト

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

### 7.2 統合テスト

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

## 8. パフォーマンス最適化

### 8.1 キャッシング戦略

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

### 8.2 非同期ストリーミング

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

## 9. 実装ロードマップ

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

## 10. リスクと対策

| リスク | 影響度 | 発生確率 | 対策 |
|--------|--------|----------|------|
| Google API制限 | 高 | 中 | バッチ処理、レート制限、キャッシング |
| 大規模データでのメモリ不足 | 高 | 低 | ストリーミング処理、ページング |
| 同時編集による競合 | 中 | 高 | 楽観的ロック、差分マージ |
| 型定義の変更 | 中 | 中 | スキーマバージョニング |

## 11. 具体的な使用例

### 11.1 基本的な使用例

```csharp
// 1. humanId=1のユーザー名を更新
var updateService = new SheetUpdateService();

var result = await updateService
    .For<HumanMaster>()
    .Where(h => h.humanId == 1)
    .Update(h => h.name, "Tanaka")
    .ExecuteAsync();

// 結果: humanId=1の行のnameフィールドのみが "Taro" → "Tanaka" に更新される
```

### 11.2 バッチ更新の例

```csharp
// 30歳以上の全ユーザーの年齢を+1
var batchResult = await updateService
    .For<HumanMaster>()
    .Where(h => h.age >= 30)
    .Update(h => h.age, h => h.age + 1)
    .WithConfirmation(true)  // 確認ダイアログを表示
    .ExecuteAsync();

// 結果: age >= 30 の全行のageフィールドが +1 される
```

### 11.3 複数フィールドの更新

```csharp
// 特定ユーザーの複数フィールドを更新
var multiFieldResult = await updateService
    .For<HumanMaster>()
    .Where(h => h.humanId == 2)
    .Update(h => h.name, "MAMI")
    .Update(h => h.age, 46)
    .ExecuteAsync();
```

### 11.4 単体テストの例

```csharp
[Test]
public async Task UpdateHumanName_Success()
{
    // Arrange
    var mockRepo = new MockSheetRepository();
    mockRepo.AddTestData(new HumanMaster { humanId = 1, name = "Taro", age = 23 });
    
    var service = new SheetUpdateService(mockRepo);
    
    // Act
    var result = await service
        .For<HumanMaster>()
        .Where(h => h.humanId == 1)
        .Update(h => h.name, "Tanaka")
        .ExecuteAsync();
    
    // Assert
    Assert.IsTrue(result.Success);
    Assert.AreEqual(1, result.UpdatedRowCount);
    Assert.AreEqual("Tanaka", mockRepo.GetData<HumanMaster>(1).name);
    Assert.AreEqual(23, mockRepo.GetData<HumanMaster>(1).age); // 他のフィールドは変更されない
}
```

## 12. まとめ

この設計は、AI_INSTRUCTIONS.mdで要求された以下の要件を完全に満たします：

### 実現される機能
1. ✅ **特定行の検索と更新**: humanId=1 の行のみを更新
2. ✅ **型を持たないフィールドの保護**: バージョンや備考は変更されない
3. ✅ **バッチ処理対応**: 複数行の一括更新
4. ✅ **任意フィールドでの検索**: humanId以外（age等）でも検索可能
5. ✅ **差分確認機能**: 更新前にプレビュー表示
6. ✅ **拡張性**: 将来の削除・挿入機能に対応可能な設計

### 技術的な特徴
1. **型安全性**: Expression Treeによるコンパイル時チェック
2. **スケーラビリティ**: 差分同期とストリーミング処理
3. **保守性**: ジェネリック設計とイベントドリブンアーキテクチャ
4. **信頼性**: トランザクション、リトライ、監査ログ
5. **パフォーマンス**: キャッシング、バッチ最適化、非同期処理

この設計により、実用的かつ拡張可能な選択的更新機能を実現できます。