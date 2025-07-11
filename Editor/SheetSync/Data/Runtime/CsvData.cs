using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using KoheiUtils;
using SheetSync.Models;
using SheetSync.Data;

namespace SheetSync
{
    [Serializable]
    public class CsvData : ICsvData
    {
        public Row[] content;

        public int row
        {
            get { return content.Length; }
        }

        public int col
        {
            get { return content.Length > 0 ? content[0].data.Length : 0; }
        }
        
        // ICsvData インターフェースの実装
        public int RowCount => row;
        public int ColumnCount => col;

        [Serializable]
        public class Row
        {
            public string[] data;

            public Row(int col)
            {
                data = new string[col];
            }

            public Row Slice(int startIndex, int endIndex = int.MaxValue)
            {
                int n = data.Length;

                if (endIndex >= n)
                {
                    endIndex = n;
                }
                else if (endIndex <= -n)
                {
                    return new Row(0);
                }
                else
                {
                    endIndex = (endIndex % n + n) % n;
                }

                if (startIndex >= endIndex)
                {
                    return new Row(0);
                }

                Row row = new Row(endIndex - startIndex);
                Array.Copy(data, startIndex, row.data, 0, row.data.Length);
                return row;
            }

            public override string ToString()
            {
                return data.ToString<string>();
            }
        }

        public CsvData()
        {
            this.content = new Row[0];
        }

        public CsvData(Row[] rows)
        {
            this.content = rows;
        }

        public CsvData Slice(int startIndex, int endIndex = int.MaxValue)
        {
            int n = content.Length;

            if (endIndex >= n)
            {
                endIndex = n;
            }
            else if (endIndex <= -n)
            {
                return new CsvData();
            }
            else
            {
                endIndex = (endIndex % n + n) % n;
            }

            if (startIndex >= endIndex)
            {
                return new CsvData();
            }

            Row[] newContent = new Row[endIndex - startIndex];
            Array.Copy(content, startIndex, newContent, 0, newContent.Length);
            return new CsvData(newContent);
        }

        public CsvData SliceColumn(int startIndex, int endIndex = int.MaxValue)
        {
            int n = col;

            if (endIndex >= n)
            {
                endIndex = n;
            }
            else if (endIndex <= -n)
            {
                return new CsvData();
            }
            else
            {
                endIndex = (endIndex % n + n) % n;
            }

            Row[] newContent = new Row[row];
            for (int i = 0; i < newContent.Length; i++)
            {
                newContent[i] = content[i].Slice(startIndex, endIndex);
            }

            return new CsvData(newContent);
        }

        public string Get(int i, int j)
        {
            return content[i].data[j];
        }

        public void Set(int i, int j, string v)
        {
            content[i].data[j] = v;
        }
        
        // ICsvData インターフェースの実装
        public string GetCell(int row, int col)
        {
            return Get(row, col);
        }
        
        public void SetCell(int row, int col, string value)
        {
            Set(row, col, value);
        }
        
        public ICsvData GetRowSlice(int startRow, int endRow = int.MaxValue)
        {
            return Slice(startRow, endRow);
        }
        
        public ICsvData GetColumnSlice(int startCol, int endCol = int.MaxValue)
        {
            return SliceColumn(startCol, endCol);
        }
        
        public IEnumerable<string> GetRow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= row)
                return Enumerable.Empty<string>();
            
            return content[rowIndex].data;
        }
        
        public IEnumerable<string> GetColumn(int colIndex)
        {
            if (colIndex < 0 || colIndex >= col)
                return Enumerable.Empty<string>();
            
            return content.Select(r => r.data[colIndex]);
        }

        public static Row[] CreateTable(int row, int col)
        {
            Row[] rows = new Row[row];
            for (int i = 0; i < row; i++)
            {
                rows[i] = new Row(col);
            }

            return rows;
        }

        public void SetFromList(List<List<string>> list)
        {
            int maxCol = -1;

            foreach (List<string> row in list)
            {
                if (row.Count > maxCol)
                {
                    maxCol = row.Count;
                }
            }

            content = CreateTable(list.Count, maxCol);

            for (int i = 0; i < row; i++)
            {
                for (int j = 0; j < col; j++)
                {
                    if (j < list[i].Count)
                    {
                        Set(i, j, list[i][j]);
                    }
                    else
                    {
                        Set(i, j, "");
                    }
                }
            }
        }
        
        // ICsvData インターフェースの実装
        public void SetFromListOfObjects(object table)
        {
            SetFromListOfListObject(table);
        }
        
        /// <summary>
        ///  list は List<List<object>> であることを期待する.
        /// </summary>
        public void SetFromListOfListObject(object table)
        {
            int maxCol = -1;

            var list = table as List<object>;
            
            foreach (var row in list)
            {
                int col = (row as List<object>).Count;
                if (col > maxCol)
                {
                    maxCol = col;
                }
            }

            content = CreateTable(list.Count, maxCol);

            for (int i = 0; i < row; i++)
            {
                for (int j = 0; j < col; j++)
                {
                    var row = list[i] as List<object>;
                    if (j < row.Count)
                    {
                        Set(i, j, row[j].ToString());
                    }
                    else
                    {
                        Set(i, j, "");
                    }
                }
            }
        }

        public override string ToString()
        {
            return ToCsvString();
        }
        
        public string ToCsvString()
        {
            string s = "";

            for (int i = 0; i < row; i++)
            {
                for (int j = 0; j < col; j++)
                {
                    string value = Get(i, j);
                    value =  value.Replace("\"", "\"\"");
                    value =  value.Replace("\r\n", "\n");
                    value =  value.Replace("\n", "\\n");
                    s     += "\"" + value + "\", ";
                }

                s =  s.Substring(0, s.Length - 2);
                s += "\n";
            }

            return s;
        }
    }
}