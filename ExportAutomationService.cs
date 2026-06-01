using Microsoft.Web.WebView2.Wpf;
using Microsoft.Web.WebView2.Core;
using System.IO;
using System.IO.Compression;

namespace DeepSeekMonitor;

public class ExportAutomationService
{
    private WebView2? _webView;
    private Action<string>? _onLog;
    private Action<List<UsageRecord>>? _onComplete;
    private string _downloadDir = "";
    private int _retry;

    private const string ClickScript = """
    (()=>{
      const vis=el=>{if(!el)return false;const r=el.getBoundingClientRect();const s=getComputedStyle(el);return r.width>0&&r.height>0&&s.visibility!=='hidden'&&s.display!=='none';};
      const txt=el=>(el?.innerText||el?.textContent||'').trim().replace(/\s+/g,' ');
      const cs=Array.from(document.querySelectorAll('button,a,[role="button"],span,div')).filter(vis);
      let best=null,bs=-1;
      for(const el of cs){
        const t=txt(el).toLowerCase();const role=(el.getAttribute('role')||'').toLowerCase();const tag=el.tagName.toLowerCase();let s=0;
        if(role==='button')s+=80;if(tag==='button')s+=60;if(t==='导出')s+=200;if(t.includes('导出'))s+=80;if(t.includes('export'))s+=40;if(t.includes('下载'))s+=30;if(t.includes('csv'))s+=20;
        if(s>bs){bs=s;best=el;}
      }
      if(best){
        try{best.scrollIntoView({block:'center'});}catch{}best.focus();
        ['pointerover','mouseover','pointerdown','mousedown','pointerup','mouseup','click'].forEach(t=>{best.dispatchEvent(new MouseEvent(t,{bubbles:true,cancelable:true,clientX:1,clientY:1}));});
        if(best.click)best.click();return JSON.stringify({ok:true,text:txt(best),score:bs});
      }
      return JSON.stringify({ok:false,needsLogin:!!document.querySelector('input[type="password"]')||/sign_in|login/i.test(location.href),url:location.href});
    })();
    """;

    public void SetWebView(WebView2 wv) => _webView = wv;

    public async Task StartAsync(Action<string> onLog, Action<List<UsageRecord>> onComplete)
    {
        _onLog = onLog; _onComplete = onComplete; _retry = 0;
        _downloadDir = Path.Combine(Path.GetTempPath(), "DeepSeekMonitor", "autoexport");
        Directory.CreateDirectory(_downloadDir);
        foreach (var f in Directory.GetFiles(_downloadDir)) try { File.Delete(f); } catch { }
        if (_webView == null) { Log("WebView 未初始化"); return; }
        await _webView.EnsureCoreWebView2Async();
        _webView.CoreWebView2.DownloadStarting -= OnDownload;
        _webView.CoreWebView2.DownloadStarting += OnDownload;
        _webView.CoreWebView2.DOMContentLoaded -= OnPageLoaded;
        _webView.CoreWebView2.DOMContentLoaded += OnPageLoaded;
        Log("正在打开 DeepSeek 用量页面...");
        _webView.CoreWebView2.Navigate("https://platform.deepseek.com/usage");
    }

    private async void OnPageLoaded(object? s, CoreWebView2DOMContentLoadedEventArgs e) { if (_webView == null) return; await Task.Delay(2000); await TryClick(); }

    private async Task TryClick()
    {
        if (_webView == null) return;
        try
        {
            var json = await _webView.CoreWebView2.ExecuteScriptAsync(ClickScript);
            json = System.Text.RegularExpressions.Regex.Unescape(json).Trim('"');
            if (json.Contains("\"ok\":true")) { Log("已触发导出，等待下载..."); return; }
            if (json.Contains("\"needsLogin\":true")) { Log("请先在浏览器窗口中登录 DeepSeek 账号"); return; }
            if (_retry < 4) { _retry++; Log($"等待导出按钮... ({_retry}/4)"); await Task.Delay(2000); await TryClick(); }
            else Log("未找到导出按钮，请手动点击");
        }
        catch (Exception ex) { Log($"执行失败: {ex.Message}"); }
    }

    private void OnDownload(object? s, CoreWebView2DownloadStartingEventArgs e)
    {
        var dest = Path.Combine(_downloadDir, Path.GetFileName(e.DownloadOperation.ResultFilePath ?? "amount.zip"));
        e.ResultFilePath = dest;
        Log($"下载: {Path.GetFileName(dest)}");
        e.DownloadOperation.StateChanged += (_, _) => { if (e.DownloadOperation.State == CoreWebView2DownloadState.Completed) { Log("下载完成"); Process(dest); } };
    }

    private void Process(string path)
    {
        try
        {
            if (!File.Exists(path)) { Log("文件未找到"); return; }
            var records = new List<UsageRecord>();
            if (path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) { var exDir = Path.Combine(_downloadDir, "extracted"); Directory.CreateDirectory(exDir); ZipFile.ExtractToDirectory(path, exDir, true); foreach (var f in Directory.GetFiles(exDir, "*.csv", SearchOption.AllDirectories)) records.AddRange(SimpleCsvParser.Parse(f)); }
            else records.AddRange(SimpleCsvParser.Parse(path));
            Log(records.Count > 0 ? $"解析 {records.Count} 条记录" : "未解析到记录");
            _onComplete?.Invoke(records);
        }
        catch (Exception ex) { Log($"处理失败: {ex.Message}"); }
    }

    private void Log(string m) => _onLog?.Invoke(m);

    public void Cleanup()
    {
        if (_webView?.CoreWebView2 != null) { _webView.CoreWebView2.DownloadStarting -= OnDownload; _webView.CoreWebView2.DOMContentLoaded -= OnPageLoaded; }
        _onLog = null; _onComplete = null; _webView = null;
    }
}
