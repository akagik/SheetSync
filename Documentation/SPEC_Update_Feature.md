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

| 操作               | 目標時間 | 最大許容時間 | 備考               |
| ------------------ | -------- | ------------ | ------------------ |
| 単一行検索         | 100ms    | 500ms        | キャッシュヒット時 |
| 単一行更新         | 200ms    | 1s           | API呼び出し含む    |
| 100行バッチ更新    | 2s       | 5s           | 並列処理使用       |
| 1000行バッチ更新   | 10s      | 30s          | 分割バッチ処理     |
| 差分計算（1000行） | 50ms     | 200ms        | メモリ上の処理     |

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