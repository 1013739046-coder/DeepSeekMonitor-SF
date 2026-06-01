using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace DeepSeekMonitor;

public class DeepSeekService
{
    private readonly HttpClient _client;

    public DeepSeekService()
    {
        _client = new HttpClient { BaseAddress = new Uri("https://api.deepseek.com"), Timeout = TimeSpan.FromSeconds(15) };
        _client.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    public bool HasApiKey { get; private set; }

    public void SetApiKey(string key)
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        HasApiKey = true;
    }

    public void ClearApiKey()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        HasApiKey = false;
    }

    public async Task<BalanceResponse> FetchBalanceAsync()
    {
        var response = await _client.GetAsync("/user/balance");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<BalanceResponse>() ?? throw new Exception("余额数据为空");
    }

    public async Task<UsageResponse> FetchUsageAsync(DateTime start, DateTime end)
    {
        var startStr = start.ToString("yyyy-MM-dd");
        var endStr = end.ToString("yyyy-MM-dd");
        var response = await _client.GetAsync($"/v1/usage?start_date={startStr}&end_date={endStr}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UsageResponse>() ?? throw new Exception("用量数据为空");
    }
}

public class ApiException : Exception { public ApiException(string m) : base(m) { } }
