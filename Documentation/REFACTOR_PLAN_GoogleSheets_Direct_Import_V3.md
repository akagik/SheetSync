# Google Sheets API 直接インポート機能 - リファクタリング計画書 V3

## 1. 現状の問題点（V1より）

### 1.1 処理フローの無駄
現在の実装では以下の二重処理が発生しています：

1. **GoogleSheetsDownloader**: 
   - Google Sheets API から `response.Values` (IList<IList<object>>) を取得
   - CSV 形式の文字列に変換
   - ファイルに保存

2. **CsvConvert**: 
   - CSV ファイルを読み込み
   - CSV 文字列をパース
   - `CsvData` オブジェクトに変換
   - アセットを生成

### 1.2 問題の影響
- **不要なファイルI/O処理**: ディスクアクセスのオーバーヘッド
- **CSV エンコード/デコードの無駄**: データ形式の不要な変換
- **メモリの二重使用**: API レスポンス → CSV 文字列 → CsvData でメモリコピーが多発
- **パフォーマンスの低下**: 特に大規模データで顕著
- **エラーの可能性増加**: ファイルアクセス権限、パス問題など

## 2. 提案する新設計

### 2.1 アーキテクチャ概要
```
Google Sheets API
    ↓
response.Values (IList<IList<object>>)
    ↓
ICsvData インターフェース（SheetData 実装）
    ↓
CreateAssets（ファイルを経由せずに処理）
```

### 2.2 主要な変更点

本来の目的である**ファイルI/Oを排除した直接インポート**を実現しつつ、メモリ効率も最適化する設計：

1. **ICsvData インターフェースの導入**: メモリコピーを最小限に
2. **SheetData クラス**: Google Sheets データを直接参照（ビューパターン）
3. **既存 CsvData の互換性維持**: ICsvData を実装して段階的移行

## 3. 詳細設計

### 3.1 ICsvData インターフェース（新規）

```csharp
namespace SheetSync.Data
{
    /// <summary>
    /// CSV データの抽象インターフェース
    /// ファイルベースと Google Sheets ベースの両方をサポート
    /// </summary>
    public interface ICsvData
    {
        // 基本プロパティ
        int RowCount { get; }
        int ColumnCount { get; }
        
        // セルアクセス
        string GetCell(int row, int col);
        void SetCell(int row, int col, string value);
        
        // スライス操作（ビューを返す）
        ICsvData GetRowSlice(int startRow, int endRow = int.MaxValue);
        ICsvData GetColumnSlice(int startCol, int endCol = int.MaxValue);
        
        // 行・列の取得
        IEnumerable<string> GetRow(int rowIndex);
        IEnumerable<string> GetColumn(int colIndex);
        
        // データ設定
        void SetFromList(List<List<string>> data);
        void SetFromListOfObjects(object table);
        
        // CSV 文字列への変換
        string ToCsvString();
    }
}
```

### 3.2 SheetData クラス（新規実装）

```csharp
namespace SheetSync.Data
{
    /// <summary>
    /// Google Sheets のデータを直接参照するクラス
    /// メモリコピーを避けるため、元データへの参照を保持
    /// </summary>
    public class SheetData : ICsvData
    {
        private readonly IList<IList<object>> _values;
        private readonly int _rowOffset;
        private readonly int _colOffset;
        private readonly int _rowCount;
        private readonly int _colCount;
        
        // コンストラクタ（ビュー作成用）
        public SheetData(IList<IList<object>> values, 
                        int rowOffset = 0, int colOffset = 0,
                        int? rowCount = null, int? colCount = null)
        {
            _values = values;
            _rowOffset = rowOffset;
            _colOffset = colOffset;
            _rowCount = rowCount ?? CalculateRowCount(values);
            _colCount = colCount ?? CalculateColumnCount(values);
        }
        
        public int RowCount => _rowCount;
        public int ColumnCount => _colCount;
        
        public string GetCell(int row, int col)
        {
            int actualRow = row + _rowOffset;
            int actualCol = col + _colOffset;
            
            if (actualRow >= _values.Count) return "";
            var rowData = _values[actualRow];
            if (actualCol >= rowData.Count) return "";
            
            return rowData[actualCol]?.ToString() ?? "";
        }
        
        public ICsvData GetRowSlice(int startRow, int endRow = int.MaxValue)
        {
            // 新しいビューを作成（データのコピーなし）
            int actualEndRow = Math.Min(endRow, _rowCount);
            return new SheetData(_values, 
                _rowOffset + startRow, _colOffset,
                actualEndRow - startRow, _colCount);
        }
        
        // 他のメソッドも同様にビューパターンで実装
    }
}
```

### 3.3 CsvData クラスの更新

既存の `CsvData` クラスを `ICsvData` インターフェースを実装するように更新：

```csharp
namespace SheetSync
{
    [Serializable]
    public class CsvData : ICsvData
    {
        // 既存のフィールドとプロパティ
        public Row[] content;
        
        // ICsvData インターフェースの実装
        public int RowCount => content.Length;
        public int ColumnCount => content.Length > 0 ? content[0].data.Length : 0;
        
        public string GetCell(int row, int col) => Get(row, col);
        public void SetCell(int row, int col, string value) => Set(row, col, value);
        
        public ICsvData GetRowSlice(int startRow, int endRow = int.MaxValue)
        {
            // 既存の Slice メソッドを活用
            return Slice(startRow, endRow);
        }
        
        // 他のメソッドも既存の実装をラップ
    }
}
```

### 3.4 ICsvDataProvider インターフェース（V1より）

```csharp
public interface ICsvDataProvider
{
    ICsvData GetCsvData();
    bool IsAvailable();
}

// 実装クラス
public class FileCsvDataProvider : ICsvDataProvider
{
    // 既存のファイルベースの実装
}

public class GoogleSheetsCsvDataProvider : ICsvDataProvider
{
    private readonly IList<IList<object>> _values;
    
    public ICsvData GetCsvData()
    {
        return new SheetData(_values);
    }
}
```

## 4. 実装計画

### Phase 1: 基盤整備（1-2日）
1. `ICsvData` インターフェースの作成
2. `ICsvDataProvider` インターフェースの作成
3. `CsvData` クラスへの `ICsvData` 実装
4. 単体テストの作成

### Phase 2: Provider 実装（2-3日）
1. `SheetData` クラスの実装（Google Sheets データの直接参照）
2. `FileCsvDataProvider` の実装（既存ロジックの移行）
3. `GoogleSheetsCsvDataProvider` の実装
4. `CsvConvert.CreateAssets` メソッドのリファクタリング

### Phase 3: 統合（2-3日）
1. `SheetSyncService` の更新
   - ダウンロード処理とインポート処理の統合
   - ファイルI/Oをバイパスする新フロー
2. `ConvertSetting` への新フラグ追加
   ```csharp
   public bool useDirectImport = false; // true の場合、ファイルを経由しない
   ```
3. 主要な利用箇所を `ICsvData` に変更
   - `CsvLogic.cs`
   - `AssetsGenerator.cs`
   - `EnumGenerator.cs`

### Phase 4: 最適化とクリーンアップ（1日）
1. 不要になったファイル保存処理の削除（オプション）
2. エラーハンドリングの改善
3. パフォーマンステストとベンチマーク
4. ドキュメントの更新

## 5. メリット（V1 + V3 の統合）

### 5.1 パフォーマンス向上
1. **ファイルI/Oの削除**（V1の主目的）
   - CSV ファイルの書き込み/読み込みを完全に排除
   - ディスクアクセスのオーバーヘッドをゼロに

2. **CSV エンコード/デコードの削除**（V1の主目的）
   - Google Sheets API レスポンス → CSV 文字列 → パースの無駄を排除
   - 直接データ構造を利用

3. **メモリ効率の大幅改善**（V3の追加価値）
   - ゼロコピービュー: `SheetData` は元データへの参照のみ保持
   - 10,000行 × 100列: 従来 約40MB → 改善後 約4MB
   - Slice 操作: 従来 データコピー → 改善後 オフセット記録のみ

### 5.2 信頼性向上
- **ファイルシステムの問題を回避**（V1の主目的）
- **パスの不一致問題を根本的に解決**（V1の主目的）
- **メモリ不足エラーのリスク低減**（V3の追加価値）

### 5.3 保守性向上
- シンプルなデータフロー
- テストの容易性向上
- 拡張性の確保（新しいデータソースを容易に追加可能）

## 6. 互換性の維持（V1より）

### 6.1 後方互換性
- 既存のファイルベースのワークフローは維持
- ConvertSetting に `useDirectImport` フラグを追加
- デフォルトは従来の動作（ファイル経由）

### 6.2 移行パス
```csharp
public class ConvertSetting
{
    // 既存のフィールド
    public bool useGSPlugin = false;
    
    // 新規追加
    public bool useDirectImport = false; // true の場合、ファイルを経由しない
}
```

## 7. リスクと対策

### 7.1 リスク
1. **大規模なスプレッドシートでのメモリ使用量**
2. **既存ワークフローへの影響**
3. **Google Sheets データの一貫性**

### 7.2 対策
1. **ストリーミング処理の検討**（将来的な拡張）
2. **段階的な移行とフィーチャーフラグによる制御**
3. **データ取得時にスナップショットを作成**

## 8. 成功基準

1. **既存の機能が全て動作すること**
2. **直接インポート使用時のパフォーマンス向上**
   - 処理時間: 30%以上の短縮
   - メモリ使用量: 90%削減
3. **ファイルパス関連のエラーがゼロになること**
4. **大規模データ（10,000行以上）でも安定動作**

## 9. タイムライン

- **Phase 1**: 1-2日（基盤作成）
- **Phase 2**: 2-3日（Provider実装とテスト）
- **Phase 3**: 2-3日（統合とテスト）
- **Phase 4**: 1日（最適化）

合計: 約1週間の開発期間

## まとめ

この V3 計画では、V1 の本来の目的である「ファイルI/Oを排除した直接インポート」を実現しつつ、メモリ効率も大幅に改善します。ICsvData インターフェースの導入により、将来の拡張性も確保しながら、段階的な移行が可能な設計となっています。