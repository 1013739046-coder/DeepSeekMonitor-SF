using System.Text.Json.Serialization;

namespace DeepSeekMonitor;

public class BalanceResponse
{
    [JsonPropertyName("is_available")]
    public bool IsAvailable { get; set; }
    [JsonPropertyName("balance_infos")]
    public List<BalanceInfo> BalanceInfos { get; set; } = [];
}

public class BalanceInfo
{
    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "";
    [JsonPropertyName("total_balance")]
    public string TotalBalance { get; set; } = "";
    [JsonPropertyName("granted_balance")]
    public string GrantedBalance { get; set; } = "";
    [JsonPropertyName("topped_up_balance")]
    public string ToppedUpBalance { get; set; } = "";
}

public class UsageResponse
{
    [JsonPropertyName("data")]
    public List<UsageRecord> Data { get; set; } = [];
}

public class UsageRecord
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";
    [JsonPropertyName("model_name")]
    public string ModelName { get; set; } = "";
    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }
    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }
    [JsonPropertyName("input_cache_hit_tokens")]
    public int InputCacheHitTokens { get; set; }
    [JsonPropertyName("input_cache_miss_tokens")]
    public int InputCacheMissTokens { get; set; }
    [JsonPropertyName("cost_in_cents")]
    public int CostInCents { get; set; }
    [JsonPropertyName("date")]
    public string Date { get; set; } = "";
    [JsonPropertyName("request_count")]
    public int RequestCount { get; set; }
}

public class ModelUsageSummary
{
    public string DisplayName { get; set; } = "";
    public string ModelName { get; set; } = "";
    public int TotalTokens { get; set; }
    public int CostInCents { get; set; }
    public int RequestCount { get; set; }
    public string TotalTokensFormatted { get => TotalTokens switch { >= 1_000_000 => $"{TotalTokens / 1_000_000.0:F1}M", >= 1_000 => $"{TotalTokens / 1_000.0:F1}K", _ => TotalTokens.ToString() }; set { _ = value; } }
    public string CostFormatted { get => $"¥{CostInCents / 100.0:F2}"; set { _ = value; } }
}

public class DashboardCache
{
    public double TotalBalance { get; set; }
    public double CurrentMonthCost { get; set; }
    public int FlashTotalTokens { get; set; }
    public int FlashCostInCents { get; set; }
    public int ProTotalTokens { get; set; }
    public int ProCostInCents { get; set; }
    public List<UsageRecord> CachedRecords { get; set; } = [];
    public DateTime LastUpdated { get; set; }
}
