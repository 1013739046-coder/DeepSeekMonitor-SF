using System.Globalization;
using System.IO;

namespace DeepSeekMonitor;

public static class SimpleCsvParser
{
    public static List<UsageRecord> Parse(string filePath)
    {
        var text = File.ReadAllText(filePath);
        var rows = ParseRows(text);
        if (rows.Count < 2) return [];
        var headers = rows[0].Select(Norm).ToList();
        if (headers.Contains("type") && headers.Contains("price") && headers.Contains("amount"))
            return ParseAmountCsv(rows, headers);
        if (headers.Contains("wallet_type") && headers.Contains("cost"))
            return ParseCostCsv(rows, headers);
        return [];
    }

    private static List<UsageRecord> ParseAmountCsv(List<List<string>> rows, List<string> headers)
    {
        var dateIdx = FindCol(headers, "utc_date", "utcdate", "date");
        var modelIdx = FindCol(headers, "model");
        var typeIdx = FindCol(headers, "type");
        var priceIdx = FindCol(headers, "price");
        var amountIdx = FindCol(headers, "amount");
        if (dateIdx < 0 || modelIdx < 0 || typeIdx < 0) return [];
        var groups = new Dictionary<string, (int Hit, int Miss, int Output, int Req, double Cost)>();
        for (int i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            var date = NormDate(Get(row, dateIdx));
            var model = NormModel(Get(row, modelIdx));
            if (date == null || model == null) continue;
            var type = Norm(Get(row, typeIdx));
            var price = ParseDouble(Get(row, priceIdx));
            var amount = ParseDouble(Get(row, amountIdx));
            var key = $"{date}|{model}";
            var agg = groups.GetValueOrDefault(key);
            if (type.Contains("requestcount")) agg.Req += (int)amount;
            else if (type.Contains("inputcachehit")) { agg.Hit += (int)amount; agg.Cost += price * amount; }
            else if (type.Contains("inputcachemiss")) { agg.Miss += (int)amount; agg.Cost += price * amount; }
            else if (type.Contains("output")) { agg.Output += (int)amount; agg.Cost += price * amount; }
            else if (type.Contains("input") || type.Contains("prompt")) { agg.Miss += (int)amount; agg.Cost += price * amount; }
            groups[key] = agg;
        }
        return groups.Select(kv =>
        {
            var parts = kv.Key.Split('|'); var agg = kv.Value;
            return new UsageRecord { Id = kv.Key, ModelName = parts[1], Date = parts[0], PromptTokens = agg.Hit + agg.Miss, InputCacheHitTokens = agg.Hit, InputCacheMissTokens = agg.Miss, CompletionTokens = agg.Output, TotalTokens = agg.Hit + agg.Miss + agg.Output, CostInCents = (int)(agg.Cost * 100), RequestCount = agg.Req };
        }).ToList();
    }

    private static List<UsageRecord> ParseCostCsv(List<List<string>> rows, List<string> headers)
    {
        var dateIdx = FindCol(headers, "utc_date", "utcdate", "date");
        var modelIdx = FindCol(headers, "model");
        var costIdx = FindCol(headers, "cost");
        if (dateIdx < 0 || modelIdx < 0 || costIdx < 0) return [];
        return rows.Skip(1).Select(row =>
        {
            var date = NormDate(Get(row, dateIdx)); var model = NormModel(Get(row, modelIdx));
            if (date == null || model == null) return null;
            var cost = ParseDouble(Get(row, costIdx));
            return new UsageRecord { Id = $"{date}|{model}|cost", ModelName = model, Date = date, CostInCents = (int)(cost * 100) };
        }).Where(r => r != null).Select(r => r!).ToList();
    }

    private static List<List<string>> ParseRows(string text)
    {
        var result = new List<List<string>>();
        foreach (var line in text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = new List<string>(); var field = ""; var inQ = false;
            for (int i = 0; i < line.Length; i++)
            {
                var c = line[i];
                if (c == '"' && !inQ) { inQ = true; continue; }
                if (c == '"' && inQ) { if (i + 1 < line.Length && line[i + 1] == '"') { field += '"'; i++; continue; } inQ = false; continue; }
                if (c == ',' && !inQ) { fields.Add(field.Trim()); field = ""; continue; }
                field += c;
            }
            fields.Add(field.Trim());
            if (fields.Any(f => f.Length > 0)) result.Add(fields);
        }
        return result;
    }

    private static string Get(List<string> row, int idx) => idx >= 0 && idx < row.Count ? row[idx] : "";
    private static int FindCol(List<string> headers, params string[] names) { for (int i = 0; i < headers.Count; i++) if (names.Any(n => headers[i].Contains(Norm(n)))) return i; return -1; }
    private static string Norm(string s) => s.Trim().ToLowerInvariant().Replace("_", "").Replace("-", "").Replace(" ", "");
    private static string? NormDate(string raw) { if (string.IsNullOrWhiteSpace(raw)) return null; raw = raw.Trim(); return raw.Length >= 10 ? raw[..10].Replace("/", "-") : null; }
    private static string NormModel(string raw) { var n = raw.Trim().ToLowerInvariant(); if (n.Contains("reasoner") || n.Contains("pro") || n.Contains("v4-pro")) return "deepseek-reasoner"; if (n.Contains("chat") || n.Contains("flash") || n.Contains("v4-flash")) return "deepseek-chat"; return raw.Trim(); }
    private static double ParseDouble(string s) => string.IsNullOrWhiteSpace(s) ? 0 : double.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;
}
