using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using SheetSync.Services.Auth;
using SheetSync.Services.Update;
using SheetSync.Services.Insert;

namespace SheetSync.Services.Common
{
    /// <summary>
    /// ExtendedSheetData を使用したサービス統合の実装例とヘルパーメソッドを提供するクラス
    /// 
    /// このクラスは、Google Sheets との双方向データ同期を実現するための実装パターンを示します。
    /// ExtendedSheetData の変更追跡機能を活用し、既存の SheetUpdateServiceAccountService や 
    /// SheetInsertServiceAccountService と連携して、効率的なデータ同期を実現します。
    /// 
    /// 主な機能:
    /// 1. スプレッドシートから ExtendedSheetData へのデータ読み込み
    /// 2. ExtendedSheetData の変更履歴を使用した差分更新
    /// 3. 挿入・更新・削除の各操作の自動振り分け
    /// 4. ヘッダー行の自動検出とビューの作成
    /// 
    /// 使用例:
    /// // データの読み込み
    /// var sheetData = await SheetDataIntegrationExample.LoadSheetDataAsync(spreadsheetId, sheetName);
    /// 
    /// // データの編集
    /// sheetData.UpdateRowByKey("ID", "123", new Dictionary<string, object> { ["Name"] = "Updated" });
    /// 
    /// // 変更の適用
    /// await SheetDataIntegrationExample.UpdateSpreadsheetWithSheetData(spreadsheetId, sheetName, sheetData);
    /// </summary>
    public static class SheetDataIntegrationExample
    {
        /// <summary>
        /// ExtendedSheetData の変更履歴を使用してスプレッドシートを更新します
        /// 
        /// このメソッドは ExtendedSheetData に記録された変更（挿入、更新、削除）を
        /// 適切なサービスに振り分けて実行します。変更は以下の順序で適用されます：
        /// 1. 削除（降順でインデックスのずれを防ぐ）
        /// 2. 挿入（新しい行の追加）
        /// 3. 更新（既存行の変更）
        /// 
        /// 注意: 現在、削除機能は未実装です。
        /// </summary>
        /// <param name="spreadsheetId">更新対象のスプレッドシートID</param>
        /// <param name="sheetName">更新対象のシート名</param>
        /// <param name="sheetData">変更が記録された ExtendedSheetData インスタンス</param>
        /// <param name="verbose">詳細ログを出力するかどうか</param>
        /// <returns>すべての更新が成功した場合は true、それ以外は false</returns>
        public static async Task<bool> UpdateSpreadsheetWithSheetData(
            string spreadsheetId,
            string sheetName,
            ExtendedSheetData sheetData,
            bool verbose = true)
        {
            try
            {
                // 1. SheetDataから変更を収集
                var changes = sheetData.Changes;
                if (changes.Count == 0)
                {
                    if (verbose) Debug.Log("変更がありません。");
                    return true;
                }
                
                // 2. 変更を種類ごとに分類
                var updates = new Dictionary<string, Dictionary<string, object>>();
                var insertions = new List<(int rowIndex, Dictionary<string, object> rowData)>();
                var deletions = new List<int>();
                
                foreach (var change in changes)
                {
                    switch (change.Type)
                    {
                        case ExtendedSheetData.ChangeType.Update:
                            // 更新の場合は行ごとにグループ化
                            var rowKey = GetRowKey(sheetData, change.RowIndex);
                            if (!string.IsNullOrEmpty(rowKey))
                            {
                                if (!updates.ContainsKey(rowKey))
                                {
                                    updates[rowKey] = new Dictionary<string, object>();
                                }
                                updates[rowKey][change.ColumnName] = change.NewValue;
                            }
                            break;
                            
                        case ExtendedSheetData.ChangeType.Insert:
                            insertions.Add((change.RowIndex, change.RowData));
                            break;
                            
                        case ExtendedSheetData.ChangeType.Delete:
                            deletions.Add(change.RowIndex);
                            break;
                    }
                }
                
                // 3. 各種サービスを使用して変更を適用
                bool success = true;
                
                // 削除を先に実行（インデックスのずれを防ぐため降順で）
                if (deletions.Count > 0)
                {
                    deletions.Sort((a, b) => b.CompareTo(a));
                    foreach (var rowIndex in deletions)
                    {
                        // TODO: DeleteServiceの実装が必要
                        if (verbose) Debug.LogWarning($"行削除はまだ実装されていません: 行 {rowIndex}");
                    }
                }
                
                // 挿入を実行
                if (insertions.Count > 0)
                {
                    var insertService = new SheetInsertServiceAccountService();
                    success &= await insertService.InsertMultipleRowsAsync(
                        spreadsheetId, sheetName, insertions, verbose);
                }
                
                // 更新を実行
                if (updates.Count > 0)
                {
                    var updateService = new SheetUpdateServiceAccountService();
                    // 仮にIDカラムをキーとして使用
                    success &= await updateService.UpdateMultipleRowsAsync(
                        spreadsheetId, sheetName, "ID", updates, verbose);
                }
                
                if (success && verbose)
                {
                    Debug.Log($"スプレッドシートの更新が完了しました: " +
                             $"更新={updates.Count}行, 挿入={insertions.Count}行, 削除={deletions.Count}行");
                }
                
                return success;
            }
            catch (Exception ex)
            {
                if (verbose) Debug.LogError($"スプレッドシート更新エラー: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Google Sheets からデータを読み込み、ExtendedSheetData インスタンスを作成します
        /// 
        /// このメソッドは以下の処理を行います：
        /// 1. サービスアカウント認証の確認
        /// 2. スプレッドシートからデータの取得
        /// 3. ヘッダー行の自動検出または GlobalCCSettings からの取得
        /// 4. ヘッダー行に基づいたデータビューの作成
        /// 
        /// ヘッダー検出の優先順位：
        /// 1. GlobalCCSettings の rowIndexOfName 設定値
        /// 2. 一般的なカラム名（key, id, name, ja, en, ko）による自動検出
        /// 3. ヒューリスティックによる推測
        /// </summary>
        /// <param name="spreadsheetId">読み込み対象のスプレッドシートID</param>
        /// <param name="sheetName">読み込み対象のシート名</param>
        /// <param name="verbose">詳細ログを出力するかどうか</param>
        /// <returns>作成された ExtendedSheetData インスタンス、エラー時は null</returns>
        public static async Task<ExtendedSheetData> LoadSheetDataAsync(
            string spreadsheetId,
            string sheetName,
            bool verbose = true)
        {
            try
            {
                if (!GoogleServiceAccountAuth.IsAuthenticated)
                {
                    if (verbose) Debug.LogError("サービスアカウント認証が必要です。");
                    return null;
                }
                
                var service = GoogleServiceAccountAuth.GetAuthenticatedService();
                
                // データを取得
                var range = $"{sheetName}!A:Z";
                var request = service.Spreadsheets.Values.Get(spreadsheetId, range);
                var response = await request.ExecuteAsync();
                
                if (response.Values == null || response.Values.Count == 0)
                {
                    if (verbose) Debug.LogError("データが見つかりません。");
                    return null;
                }
                
                // ExtendedSheetDataを作成
                var sheetData = new ExtendedSheetData(response.Values);
                
                // まず GlobalCCSettings から取得を試みる
                var headerRowIndex = HeaderDetector.GetHeaderRowIndexFromSettings();
                
                // 設定値が範囲外の場合は自動検出
                if (headerRowIndex >= response.Values.Count || headerRowIndex < 0)
                {
                    if (verbose) Debug.LogWarning($"GlobalCCSettings の rowIndexOfName ({headerRowIndex}) が範囲外です。自動検出を行います。");
                    
                    // ヘッダー行を検出してビューを作成
                    var commonColumns = new[] { "key", "id", "name", "ja", "en", "ko" };
                    headerRowIndex = HeaderDetector.DetectHeaderRowByMultipleColumns(response.Values, commonColumns, 2);
                    
                    if (headerRowIndex == -1)
                    {
                        headerRowIndex = HeaderDetector.GuessHeaderRow(response.Values);
                    }
                }
                
                if (headerRowIndex > 0)
                {
                    // ヘッダー行が最初の行でない場合は、ヘッダー行から始まるビューを作成
                    var dataFromHeader = response.Values.Skip(headerRowIndex).ToList();
                    sheetData = new ExtendedSheetData(dataFromHeader);
                }
                
                if (verbose)
                {
                    Debug.Log($"SheetDataを読み込みました: {sheetData.RowCount}行 x {sheetData.ColumnCount}列 (ヘッダー行: {headerRowIndex + 1})");
                }
                
                return sheetData;
            }
            catch (Exception ex)
            {
                if (verbose) Debug.LogError($"SheetData読み込みエラー: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// ExtendedSheetData を使用したデータ編集と差分表示の実装例
        /// 
        /// このメソッドは以下の一連の操作を実演します：
        /// 1. スプレッドシートからデータを読み込み
        /// 2. キーインデックスの構築（高速検索用）
        /// 3. データの編集（更新、挿入）
        /// 4. 変更差分の表示
        /// 5. 変更の適用
        /// 
        /// このメソッドは実際のアプリケーションでの使用パターンを示すサンプルです。
        /// 実際の実装では、ユーザー確認のダイアログを表示するなどの処理を追加してください。
        /// </summary>
        /// <param name="spreadsheetId">操作対象のスプレッドシートID</param>
        /// <param name="sheetName">操作対象のシート名</param>
        /// <returns>すべての操作が成功した場合は true、それ以外は false</returns>
        public static async Task<bool> ExampleEditAndShowDiff(
            string spreadsheetId,
            string sheetName)
        {
            // 1. データを読み込み
            var sheetData = await LoadSheetDataAsync(spreadsheetId, sheetName);
            if (sheetData == null) return false;
            
            // 2. キーインデックスを構築
            sheetData.BuildKeyIndex("ID");
            
            // 3. データを編集
            // 例: ID=2の行の名前と年齢を更新
            sheetData.UpdateRowByKey("ID", "2", new Dictionary<string, object>
            {
                ["Name"] = "Bob Updated",
                ["Age"] = "31"
            });
            
            // 例: 新しい行を挿入
            sheetData.InsertRow(4, new Dictionary<string, object>
            {
                ["ID"] = "10",
                ["Name"] = "New Person",
                ["Age"] = "25"
            });
            
            // 4. 差分を表示
            Debug.Log("=== 変更差分 ===");
            foreach (var change in sheetData.Changes)
            {
                switch (change.Type)
                {
                    case ExtendedSheetData.ChangeType.Update:
                        Debug.Log($"[更新] 行{change.RowIndex}, {change.ColumnName}: " +
                                 $"{change.OldValue} → {change.NewValue}");
                        break;
                        
                    case ExtendedSheetData.ChangeType.Insert:
                        Debug.Log($"[挿入] 行{change.RowIndex}: " +
                                 string.Join(", ", change.RowData));
                        break;
                        
                    case ExtendedSheetData.ChangeType.Delete:
                        Debug.Log($"[削除] 行{change.RowIndex}");
                        break;
                }
            }
            
            // 5. ユーザーに確認を求める（実際のUIでは確認ダイアログを表示）
            Debug.Log("変更を適用しますか？ (この例では自動的に適用されます)");
            
            // 6. 変更を適用
            return await UpdateSpreadsheetWithSheetData(spreadsheetId, sheetName, sheetData);
        }
        
        /// <summary>
        /// 指定された行のキー値を取得するヘルパーメソッド
        /// 
        /// このメソッドは ID カラムの値を行のキーとして使用します。
        /// UpdateSpreadsheetWithSheetData メソッド内で、更新対象の行を
        /// 特定するために使用されます。
        /// </summary>
        /// <param name="sheetData">データを含む ExtendedSheetData インスタンス</param>
        /// <param name="rowIndex">キー値を取得する行のインデックス</param>
        /// <returns>ID カラムの値（文字列）、取得できない場合は null</returns>
        private static string GetRowKey(ExtendedSheetData sheetData, int rowIndex)
        {
            // この例ではIDカラムを使用
            var idColumnIndex = sheetData.GetColumnIndex("ID");
            if (idColumnIndex >= 0 && rowIndex < sheetData.EditedValues.Count)
            {
                var row = sheetData.EditedValues[rowIndex];
                if (row != null && idColumnIndex < row.Count)
                {
                    return row[idColumnIndex]?.ToString();
                }
            }
            return null;
        }
    }
}