using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DeepSeekMonitor;

public class DailyBar
{
    public string Day { get; set; } = "";
    public int Tokens { get; set; }
    public double BarHeight { get; set; }
    public string Tip { get; set; } = "";
}

public class DashboardViewModel : INotifyPropertyChanged
{
    private readonly DeepSeekService _service = new();
    private readonly System.Timers.Timer _timer;
    private bool _isRefreshing;

    public DashboardViewModel()
    {
        _timer = new System.Timers.Timer(60_000);
        _timer.Elapsed += async (_, _) => await RefreshAsync();
        _timer.AutoReset = true;
        _numberFontSize = AppSettings.Current.NumberFontSize;
        _labelFontSize = AppSettings.Current.LabelFontSize;
        LoadCachedData();
    }

    private double _totalBalance;
    public double TotalBalance { get => _totalBalance; set { _totalBalance = value; OnPropertyChanged(); OnPropertyChanged(nameof(TotalBalanceFormatted)); OnPropertyChanged(nameof(IsAccountAvailable)); OnPropertyChanged(nameof(AccountStatusText)); } }
    public string TotalBalanceFormatted => $"¥{TotalBalance:F2}";
    public bool IsAccountAvailable => TotalBalance > 0;
    public string AccountStatusText => IsAccountAvailable ? "账户可用" : "账户不可用";

    private ModelUsageSummary? _flashUsage;
    public ModelUsageSummary? FlashUsage { get => _flashUsage; set { _flashUsage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasFlashData)); OnPropertyChanged(nameof(FlashTokensText)); OnPropertyChanged(nameof(FlashRequestsText)); OnPropertyChanged(nameof(FlashCostFormatted)); } }
    private ModelUsageSummary? _proUsage;
    public ModelUsageSummary? ProUsage { get => _proUsage; set { _proUsage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasProData)); OnPropertyChanged(nameof(ProTokensText)); OnPropertyChanged(nameof(ProRequestsText)); OnPropertyChanged(nameof(ProCostFormatted)); } }
    public bool HasFlashData => FlashUsage != null;
    public bool HasProData => ProUsage != null;
    public string ProTokensText => ProUsage?.TotalTokensFormatted ?? "0";
    public string ProRequestsText => ProUsage != null ? $"{ProUsage.RequestCount:N0} 次请求" : "";
    public string ProCostFormatted => ProUsage?.CostFormatted ?? "¥0.00";
    public string FlashTokensText => FlashUsage?.TotalTokensFormatted ?? "0";
    public string FlashRequestsText => FlashUsage != null ? $"{FlashUsage.RequestCount:N0} 次请求" : "";
    public string FlashCostFormatted => FlashUsage?.CostFormatted ?? "¥0.00";

    private bool _isLoading;
    public bool IsLoading { get => _isLoading; set { _isLoading = value; OnPropertyChanged(); OnPropertyChanged(nameof(BottomStatusText)); } }
    private string _statusText = "就绪";
    public string StatusText { get => _statusText; set { _statusText = value; OnPropertyChanged(); OnPropertyChanged(nameof(BottomStatusText)); } }
    private string _errorText = "";
    public string ErrorText { get => _errorText; set { _errorText = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasError)); OnPropertyChanged(nameof(BottomStatusText)); } }
    public bool HasError => !string.IsNullOrEmpty(ErrorText);
    private string _lastUpdated = "尚未刷新";
    public string LastUpdated { get => _lastUpdated; set { _lastUpdated = value; OnPropertyChanged(); OnPropertyChanged(nameof(BottomStatusText)); } }
    private double _currentMonthCost;
    public double CurrentMonthCost { get => _currentMonthCost; set { _currentMonthCost = value; OnPropertyChanged(); OnPropertyChanged(nameof(CurrentMonthCostFormatted)); } }
    public string CurrentMonthCostFormatted => $"¥{CurrentMonthCost:F2}";
    public string BottomStatusText { get { if (HasError) return ErrorText; if (IsLoading) return "更新中..."; return $"{StatusText} · {LastUpdated}"; } }

    private List<DailyBar> _dailyBars = [];
    public List<DailyBar> DailyBars { get => _dailyBars; set { _dailyBars = value; OnPropertyChanged(); } }

    private double _numberFontSize;
    public double NumberFontSize { get => _numberFontSize; set { _numberFontSize = value; OnPropertyChanged(); SaveFontSettings(); } }
    private double _labelFontSize;
    public double LabelFontSize { get => _labelFontSize; set { _labelFontSize = value; OnPropertyChanged(); SaveFontSettings(); } }
    private void SaveFontSettings() { AppSettings.Current.NumberFontSize = _numberFontSize; AppSettings.Current.LabelFontSize = _labelFontSize; AppSettings.Save(); }

    private bool _hasApiKey;
    public bool HasApiKey { get => _hasApiKey; set { _hasApiKey = value; OnPropertyChanged(); } }
    public DeepSeekService Service => _service;
    public List<UsageRecord> CachedRecords { get; private set; } = [];

    public async Task SetApiKeyAsync(string key) { _service.SetApiKey(key); HasApiKey = true; await RefreshAsync(); }
    public void StartAutoRefresh() => _timer.Start();
    public void StopAutoRefresh() => _timer.Stop();

    public void ResetAllData()
    {
        TotalBalance = 0; CurrentMonthCost = 0; FlashUsage = null; ProUsage = null;
        CachedRecords = []; DailyBars = []; LastUpdated = "尚未刷新"; StatusText = "就绪"; ErrorText = "";
    }

    public void ClearApiKey() { _service.ClearApiKey(); HasApiKey = false; LocalCache.Clear(); ResetAllData(); }

    public async Task RefreshAsync()
    {
        if (_isRefreshing) return;
        if (!_service.HasApiKey) { StatusText = "请先设置 API Key"; return; }
        _isRefreshing = true; IsLoading = true; ErrorText = "";
        try
        {
            var balance = await _service.FetchBalanceAsync();
            if (balance.BalanceInfos.Count > 0) TotalBalance = double.TryParse(balance.BalanceInfos[0].TotalBalance, out var tb) ? tb : 0;
            try { ApplyUsageRecords((await _service.FetchUsageAsync(DateTime.Today.AddDays(-6), DateTime.Today)).Data); StatusText = "已刷新"; }
            catch (Exception) { if (CachedRecords.Count > 0) { ApplyUsageRecords(CachedRecords); StatusText = "用量接口暂不可用，显示缓存数据"; } else { ClearUsageData(); StatusText = "用量接口暂不可用，请导入 CSV"; } }
            LastUpdated = DateTime.Now.ToString("HH:mm:ss"); SaveCache();
        }
        catch (Exception ex) { ErrorText = ex.Message; }
        finally { IsLoading = false; _isRefreshing = false; }
    }

    public void ImportAutoExport(List<UsageRecord> records) { ApplyUsageRecords(records); SaveCache(); LastUpdated = DateTime.Now.ToString("HH:mm:ss"); StatusText = $"自动导出成功，共 {records.Count} 条记录"; ErrorText = ""; }

    public void ImportCsv(string[] filePaths)
    {
        try
        {
            var all = new List<UsageRecord>();
            foreach (var p in filePaths) all.AddRange(SimpleCsvParser.Parse(p));
            if (all.Count == 0) { ErrorText = "CSV 中没有可导入的记录"; return; }
            var merged = new Dictionary<string, UsageRecord>();
            foreach (var r in all)
            {
                var k = $"{r.Date}|{r.ModelName}";
                if (merged.TryGetValue(k, out var e))
                {
                    if (e.TotalTokens == 0 && r.TotalTokens > 0) { e.TotalTokens = r.TotalTokens; e.PromptTokens = r.PromptTokens; e.CompletionTokens = r.CompletionTokens; e.InputCacheHitTokens = r.InputCacheHitTokens; e.InputCacheMissTokens = r.InputCacheMissTokens; e.RequestCount = r.RequestCount; }
                    if (e.CostInCents == 0 && r.CostInCents > 0) e.CostInCents = r.CostInCents;
                    else if (r.CostInCents > 0 && e.CostInCents > 0 && r.TotalTokens == 0) e.CostInCents = r.CostInCents;
                }
                else merged[k] = new UsageRecord { Id = k, ModelName = r.ModelName, Date = r.Date, TotalTokens = r.TotalTokens, PromptTokens = r.PromptTokens, CompletionTokens = r.CompletionTokens, InputCacheHitTokens = r.InputCacheHitTokens, InputCacheMissTokens = r.InputCacheMissTokens, CostInCents = r.CostInCents, RequestCount = r.RequestCount };
            }
            var unique = merged.Values.OrderBy(r => r.Date).ToList();
            ApplyUsageRecords(unique); SaveCache();
            LastUpdated = DateTime.Now.ToString("HH:mm:ss");
            StatusText = $"已导入 {unique.Count} 条记录"; ErrorText = "";
        }
        catch (Exception ex) { ErrorText = $"导入失败: {ex.Message}"; }
    }

    private void ApplyUsageRecords(List<UsageRecord> records) { CachedRecords = records; AggregateUsage(records); CurrentMonthCost = ComputeMonthCost(records); BuildDailyBars(records); }
    private void ClearUsageData() { FlashUsage = null; ProUsage = null; CurrentMonthCost = 0; DailyBars = []; }

    private void BuildDailyBars(List<UsageRecord> records)
    {
        var today = DateTime.Today; var bars = new List<DailyBar>();
        var daily = new Dictionary<string, (int tokens, double flash, double pro)>();
        foreach (var r in records)
        {
            if (DateTime.TryParse(r.Date, out var d) && d >= today.AddDays(-6))
            {
                var key = d.ToString("yyyy-MM-dd"); var cur = daily.GetValueOrDefault(key);
                cur.tokens += r.TotalTokens;
                if (IsFlash(r.ModelName)) cur.flash += r.CostInCents / 100.0;
                else if (IsPro(r.ModelName)) cur.pro += r.CostInCents / 100.0;
                daily[key] = cur;
            }
        }
        for (int i = 6; i >= 0; i--) { var day = today.AddDays(-i); var key = day.ToString("yyyy-MM-dd"); var d = daily.GetValueOrDefault(key); var total = d.flash + d.pro; bars.Add(new DailyBar { Day = $"{day.Month}/{day.Day}", Tokens = d.tokens, Tip = $"{day.Year}-{day.Month}-{day.Day}:\npro  {d.pro,10:F2}元\nflash{d.flash,10:F2}元\n     {total,10:F2}元" }); }
        var maxT = bars.Max(b => b.Tokens); if (maxT == 0) maxT = 1;
        foreach (var b in bars) b.BarHeight = Math.Max(4, (double)b.Tokens / maxT * 60);
        DailyBars = bars;
    }

    private void AggregateUsage(List<UsageRecord> records) { var f = records.Where(r => IsFlash(r.ModelName)).ToList(); var p = records.Where(r => IsPro(r.ModelName)).ToList(); FlashUsage = f.Count > 0 ? Summarize("V4 Flash", f) : null; ProUsage = p.Count > 0 ? Summarize("V4 Pro", p) : null; }
    private static ModelUsageSummary Summarize(string name, List<UsageRecord> r) => new() { DisplayName = name, ModelName = r[0].ModelName, TotalTokens = r.Sum(x => x.TotalTokens), CostInCents = r.Sum(x => x.CostInCents), RequestCount = r.Sum(x => x.RequestCount) };
    private static double ComputeMonthCost(List<UsageRecord> r) { var n = DateTime.Now; return r.Where(x => DateTime.TryParse(x.Date, out var d) && d.Year == n.Year && d.Month == n.Month).Sum(x => x.CostInCents) / 100.0; }
    private static bool IsFlash(string n) { var s = n.ToLower(); return s.Contains("chat") || s.Contains("flash"); }
    private static bool IsPro(string n) { var s = n.ToLower(); return s.Contains("reasoner") || s.Contains("pro"); }

    private void SaveCache() => LocalCache.Save(new DashboardCache { TotalBalance = TotalBalance, CurrentMonthCost = CurrentMonthCost, FlashTotalTokens = FlashUsage?.TotalTokens ?? 0, FlashCostInCents = FlashUsage?.CostInCents ?? 0, ProTotalTokens = ProUsage?.TotalTokens ?? 0, ProCostInCents = ProUsage?.CostInCents ?? 0, CachedRecords = CachedRecords, LastUpdated = DateTime.Now });

    private void LoadCachedData()
    {
        var cache = LocalCache.Load(); if (cache == null) return;
        TotalBalance = cache.TotalBalance; LastUpdated = cache.LastUpdated.ToString("HH:mm:ss"); CachedRecords = cache.CachedRecords;
        if (cache.FlashTotalTokens > 0 || cache.FlashCostInCents > 0) FlashUsage = new ModelUsageSummary { DisplayName = "V4 Flash", TotalTokens = cache.FlashTotalTokens, CostInCents = cache.FlashCostInCents };
        if (cache.ProTotalTokens > 0 || cache.ProCostInCents > 0) ProUsage = new ModelUsageSummary { DisplayName = "V4 Pro", TotalTokens = cache.ProTotalTokens, CostInCents = cache.ProCostInCents };
        if (CachedRecords.Count > 0) { AggregateUsage(CachedRecords); CurrentMonthCost = ComputeMonthCost(CachedRecords); BuildDailyBars(CachedRecords); }
        else CurrentMonthCost = cache.CurrentMonthCost;
        StatusText = "已从缓存恢复";
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
