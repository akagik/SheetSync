# Google Sheets API 直接インポート機能 - リファクタリング計画書

## 1. 現状の問題点

### 1.1 処理フローの無駄
現在の実装では以下の二重処理が発生しています：

1. **GoogleSheetsDownloader**: 
   - Google Sheets API から `response.Values` (List<IList<object>>) を取得
   - CSV 形式の文字列に変換
   - ファイルに保存

2. **CsvConvert**: 
   - CSV ファイルを読み込み
   - CSV 文字列をパース
   - `CsvData` オブジェクトに変換
   - アセットを生成

### 1.2 問題の影響
- 不要なファイルI/O処理
- CSV エンコード/デコードの無駄
- パフォーマンスの低下
- エラーの可能性増加（ファイルアクセス権限、パス問題など）

## 2. 提案する新設計

### 2.1 アーキテクチャ概要
```
Google Sheets API
    ↓
response.Values (List<IList<object>>)
    ↓
CsvData オブジェクト（メモリ上で直接変換）
    ↓
CreateAssets（ファイルを経由せずに処理）
```

### 2.2 主要な変更点

#### 2.2.1 新しいインターフェースの追加
```csharp
public interface ICsvDataProvider
{
    CsvData GetCsvData();
    bool IsAvailable();
}
```

#### 2.2.2 実装クラス
1. **FileCsvDataProvider**: 既存のファイルベースの実装
2. **GoogleSheetsCsvDataProvider**: API レスポンスから直接変換

### 2.3 CsvData クラスの拡張
```csharp
public static class CsvDataFactory
{
    /// <summary>
    /// Google Sheets API のレスポンスから CsvData を作成
    /// </summary>
    public static CsvData CreateFromGoogleSheetsResponse(IList<IList<object>> values)
    {
        if (values == null || values.Count == 0)
            return new CsvData();
        
        var rows = new CsvData.Row[values.Count];
        int maxColumns = values.Max(row => row?.Count ?? 0);
        
        for (int i = 0; i < values.Count; i++)
        {
            rows[i] = new CsvData.Row(maxColumns);
            var sourceRow = values[i];
            
            for (int j = 0; j < maxColumns; j++)
            {
                if (sourceRow != null && j < sourceRow.Count && sourceRow[j] != null)
                {
                    rows[i].data[j] = sourceRow[j].ToString();
                }
                else
                {
                    rows[i].data[j] = "";
                }
            }
        }
        
        return new CsvData(rows);
    }
}
```

## 3. 実装手順

### Phase 1: 基盤整備（影響範囲：小）
1. `ICsvDataProvider` インターフェースの作成
2. `CsvDataFactory` クラスの実装
3. 既存の `SetFromListOfListObject` メソッドのリファクタリング

### Phase 2: Provider 実装（影響範囲：中）
1. `FileCsvDataProvider` の実装（既存ロジックの移行）
2. `GoogleSheetsCsvDataProvider` の実装
3. `CsvConvert.CreateAssets` メソッドのリファクタリング

### Phase 3: 統合（影響範囲：大）
1. `SheetSyncService` の更新
2. ダウンロード処理とインポート処理の統合
3. ConvertSetting への新フラグ追加（直接インポートの有効/無効）

### Phase 4: 最適化とクリーンアップ
1. 不要になったファイル保存処理の削除（オプション）
2. エラーハンドリングの改善
3. パフォーマンステストとベンチマーク

## 4. 互換性の維持

### 4.1 後方互換性
- 既存のファイルベースのワークフローは維持
- ConvertSetting に `useDirectImport` フラグを追加
- デフォルトは従来の動作（ファイル経由）

### 4.2 移行パス
```csharp
public class ConvertSetting
{
    // 既存のフィールド
    public bool useGSPlugin = false;
    
    // 新規追加
    public bool useDirectImport = false; // true の場合、ファイルを経由しない
}
```

## 5. メリット

1. **パフォーマンス向上**
   - ファイルI/Oの削除
   - CSV エンコード/デコードの削除

2. **信頼性向上**
   - ファイルシステムの問題を回避
   - パスの不一致問題を根本的に解決

3. **保守性向上**
   - シンプルなデータフロー
   - テストの容易性向上

## 6. リスクと対策

### 6.1 リスク
1. 大規模なスプレッドシートでのメモリ使用量増加
2. 既存ワークフローへの影響

### 6.2 対策
1. ストリーミング処理の検討（将来的な拡張）
2. 段階的な移行とフィーチャーフラグによる制御

## 7. タイムライン

- **Phase 1**: 1日（基盤作成）
- **Phase 2**: 2日（Provider実装とテスト）
- **Phase 3**: 2日（統合とテスト）
- **Phase 4**: 1日（最適化）

合計: 約1週間の開発期間

## 8. 成功基準

1. 既存の機能が全て動作すること
2. 直接インポート使用時に30%以上のパフォーマンス向上
3. ファイルパス関連のエラーがゼロになること