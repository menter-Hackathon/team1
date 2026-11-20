using System;
using System.Collections.Generic;
using System.Text;

namespace Team1.Achievements
{
    /// <summary>CSV を読む（UTF-8 の BOM、ダブルクォート、改行を含む値に対応）。1行目は見出し</summary>
    public sealed class CsvTable
    {
        readonly Dictionary<string, int> _columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public List<string[]> Rows { get; } = new List<string[]>();

        public static CsvTable Parse(string text)
        {
            var table = new CsvTable();
            var lines = Split(text ?? "");
            if (lines.Count == 0) return table;
            for (int i = 0; i < lines[0].Length; i++) table._columns[lines[0][i].Trim()] = i;
            for (int i = 1; i < lines.Count; i++)
            {
                if (lines[i].Length == 1 && string.IsNullOrWhiteSpace(lines[i][0])) continue; // 空行
                table.Rows.Add(lines[i]);
            }
            return table;
        }

        public bool Has(string column) => _columns.ContainsKey(column);

        public string Get(string[] row, string column)
        {
            if (!_columns.TryGetValue(column, out int i) || i >= row.Length) return "";
            return row[i].Trim();
        }

        static List<string[]> Split(string text)
        {
            if (text.Length > 0 && text[0] == '﻿') text = text.Substring(1);
            var rows = new List<string[]>();
            var row = new List<string>();
            var cell = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (quoted)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                        else quoted = false;
                    }
                    else cell.Append(c);
                    continue;
                }
                switch (c)
                {
                    case '"': quoted = true; break;
                    case ',': row.Add(cell.ToString()); cell.Clear(); break;
                    case '\r': break;
                    case '\n':
                        row.Add(cell.ToString()); cell.Clear();
                        rows.Add(row.ToArray()); row.Clear();
                        break;
                    default: cell.Append(c); break;
                }
            }
            if (cell.Length > 0 || row.Count > 0)
            {
                row.Add(cell.ToString());
                rows.Add(row.ToArray());
            }
            return rows;
        }
    }
}
