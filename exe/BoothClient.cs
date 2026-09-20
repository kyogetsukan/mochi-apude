using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace MochiApude;

public class NotLoggedInException : Exception
{
    public NotLoggedInException() : base("BOOTH にログインしていない") { }
}

/// <summary>
/// WebView2 を使って BOOTH のページを「本人のブラウザとして」読む。
/// Cookie は WebView2 のプロファイル（AppState.ProfileDir）に残る。
/// このクラスは ID・パスワードを一切扱わない。ログインは LoginForm で人がやる。
/// </summary>
public class BoothClient
{
    readonly WebView2 _wv;
    public static readonly string[] LibraryTabs =
    {
        "https://accounts.booth.pm/library",
        "https://accounts.booth.pm/library/gifts",
        "https://accounts.booth.pm/library/free_downloads",
    };
    const int MaxPagesPerTab = 30;

    BoothClient(WebView2 wv) { _wv = wv; }

    public static async Task<CoreWebView2Environment> CreateEnvironmentAsync()
    {
        Directory.CreateDirectory(AppState.ProfileDir);
        return await CoreWebView2Environment.CreateAsync(null, AppState.ProfileDir);
    }

    public static async Task<BoothClient> CreateAsync(Control host)
    {
        var wv = new WebView2 { Dock = DockStyle.Fill };
        host.Controls.Add(wv);
        var env = await CreateEnvironmentAsync();
        await wv.EnsureCoreWebView2Async(env);
        wv.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false;
        return new BoothClient(wv);
    }

    /// <summary>ページを開いて HTML を返す。ログイン画面に飛ばされたら NotLoggedInException</summary>
    public async Task<string> GetHtmlAsync(string url, CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Handler(object? s, CoreWebView2NavigationCompletedEventArgs e) => tcs.TrySetResult(e.IsSuccess);
        _wv.CoreWebView2.NavigationCompleted += Handler;
        try
        {
            _wv.CoreWebView2.Navigate(url);
            using var reg = ct.Register(() => tcs.TrySetCanceled());
            var ok = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(40), ct);
            if (!ok) throw new Exception($"ページを開けない: {url}");
        }
        finally
        {
            _wv.CoreWebView2.NavigationCompleted -= Handler;
        }

        var current = _wv.CoreWebView2.Source ?? "";
        if (current.Contains("sign_in") || current.Contains("/login")) throw new NotLoggedInException();

        var json = await _wv.CoreWebView2.ExecuteScriptAsync("document.documentElement.outerHTML");
        var html = JsonSerializer.Deserialize<string>(json) ?? "";
        if (html.Contains("name=\"user[email]\"") && !html.Contains("js-download-button")) throw new NotLoggedInException();
        return html;
    }

    /// <summary>ライブラリ三タブを全ページ読み、商品 ID → (タイトル, ファイル一覧)</summary>
    public async Task<Dictionary<string, (string? Title, List<FileEntry> Files)>> ScanLibraryAsync(
        Action<string>? progress, CancellationToken ct = default)
    {
        var index = new Dictionary<string, (string? Title, List<FileEntry> Files)>();
        foreach (var tab in LibraryTabs)
        {
            for (int page = 1; page <= MaxPagesPerTab; page++)
            {
                var url = $"{tab}?page={page}";
                progress?.Invoke(url);
                var html = await GetHtmlAsync(url, ct);
                var files = BoothParser.ParseDownloadables(html);
                if (files.Count == 0) break;
                foreach (var f in files)
                {
                    if (f.ItemId == null) continue;
                    if (!index.TryGetValue(f.ItemId, out var e))
                        index[f.ItemId] = e = (f.Title, new List<FileEntry>());
                    if (e.Title == null && f.Title != null) index[f.ItemId] = e = (f.Title, e.Files);
                    if (!e.Files.Any(x => x.Id == f.Id))
                        e.Files.Add(new FileEntry { Id = f.Id, Href = f.Href, Name = f.Name });
                }
                await Task.Delay(800, ct); // 人が手で開く程度の間隔
            }
        }
        return index;
    }

    /// <summary>商品ページを読み、購入済みなら注文 ID を返す（自分の購入のみ。ギフトはここに出ない）</summary>
    public async Task<(bool LoggedIn, bool Purchased, string? Title, List<string> OrderIds)> InspectItemPageAsync(string itemId, CancellationToken ct = default)
    {
        var html = await GetHtmlAsync($"https://booth.pm/ja/items/{itemId}", ct);
        var title = System.Text.RegularExpressions.Regex.Match(html, @"<title>([^<]*)</title>") is { Success: true } t
            ? System.Net.WebUtility.HtmlDecode(t.Groups[1].Value).Replace(" - BOOTH", "").Trim() : null;
        var orderIds = System.Text.RegularExpressions.Regex.Matches(html, @"accounts\.booth\.pm/orders/(\d+)")
            .Select(m => m.Groups[1].Value).Distinct().ToList();
        bool purchased = orderIds.Count > 0 || html.Contains("購入済み");
        return (true, purchased, title, orderIds);
    }

    public async Task<(string? Title, string? ItemId, List<FileEntry> Files)> InspectOrderAsync(string orderId, CancellationToken ct = default)
    {
        var html = await GetHtmlAsync($"https://accounts.booth.pm/orders/{orderId}", ct);
        var files = BoothParser.ParseDownloadables(html);
        return (
            files.FirstOrDefault(f => f.Title != null)?.Title,
            files.FirstOrDefault(f => f.ItemId != null)?.ItemId,
            files.Select(f => new FileEntry { Id = f.Id, Href = f.Href, Name = f.Name }).ToList());
    }

    /// <summary>downloadables/… を開いて指定パスに保存する</summary>
    public async Task DownloadAsync(string href, string targetPath, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        try { if (File.Exists(targetPath)) File.Delete(targetPath); } catch { }
        var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnDownloadStarting(object? s, CoreWebView2DownloadStartingEventArgs e)
        {
            e.ResultFilePath = targetPath;
            e.Handled = true; // 既定のダウンロード UI を出さない
            var op = e.DownloadOperation;
            op.StateChanged += (_, _) =>
            {
                if (op.State == CoreWebView2DownloadState.Completed) done.TrySetResult(true);
                else if (op.State == CoreWebView2DownloadState.Interrupted)
                    done.TrySetException(new Exception($"ダウンロード中断: {op.InterruptReason}"));
            };
        }

        _wv.CoreWebView2.DownloadStarting += OnDownloadStarting;
        try
        {
            _wv.CoreWebView2.Navigate(href);
            using var reg = ct.Register(() => done.TrySetCanceled());
            await done.Task.WaitAsync(TimeSpan.FromMinutes(30), ct);
        }
        finally
        {
            _wv.CoreWebView2.DownloadStarting -= OnDownloadStarting;
        }
    }
}
