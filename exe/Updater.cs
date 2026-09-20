using System.Text.RegularExpressions;

namespace MochiApude;

public record CheckError(string Item, string Error);
public record Downloaded(string Item, string File, string Path);
public record Pending(string Item, List<string> Files);

public class CheckResult
{
    public int Checked { get; set; }
    public bool LoggedIn { get; set; } = true;
    public List<Downloaded> Downloaded { get; set; } = new();
    public List<Pending> Pending { get; set; } = new();
    public List<CheckError> Errors { get; set; } = new();
    public string DownloadDir { get; set; } = "";
}

public static class Updater
{
    public static string AddItem(AppState state, string url)
    {
        var orderId = BoothParser.ParseOrderId(url);
        var itemId = BoothParser.ParseItemId(url);
        if (orderId == null && itemId == null)
            return "商品 URL（booth.pm/…/items/数字）か注文詳細 URL（accounts.booth.pm/orders/数字）を貼ってください。";
        var item = new TrackedItem { ItemId = itemId, OrderId = orderId };
        if (state.Items.ContainsKey(item.Key)) return "既に登録済み。";
        state.Items[item.Key] = item;
        return "追加した。";
    }

    public static string RemoveItem(AppState state, string urlOrKey)
    {
        var orderId = BoothParser.ParseOrderId(urlOrKey);
        var itemId = BoothParser.ParseItemId(urlOrKey);
        var key = orderId != null ? $"order:{orderId}" : itemId != null ? $"item:{itemId}" : urlOrKey;
        return state.Items.Remove(key) ? "削除した。" : "登録されていない。";
    }

    /// <summary>
    /// 単一商品モード（もちふぃった～専用版などが使う）。
    /// 商品ページ→注文詳細だけを見てダウンロードする。ライブラリ走査なしで速い。
    /// 状態ファイルの登録一覧には触れない。落としたファイルは常に返す（Unity 側が取り込みを判断）。
    /// </summary>
    public static async Task<CheckResult> CheckSingleItemAsync(BoothClient client, AppState state, string itemId, CancellationToken ct = default)
    {
        var result = new CheckResult { DownloadDir = state.Settings.DownloadDir };
        try
        {
            var page = await client.InspectItemPageAsync(itemId, ct);
            if (!page.Purchased)
            {
                result.Errors.Add(new CheckError(page.Title ?? itemId, "購入が確認できない（ギフト受取品はこの方式では取得できない）"));
                return result;
            }
            var files = new List<FileEntry>();
            foreach (var oid in page.OrderIds)
            {
                var o = await client.InspectOrderAsync(oid, ct);
                foreach (var f in o.Files) if (!files.Any(x => x.Id == f.Id)) files.Add(f);
                await Task.Delay(400, ct);
            }
            result.Checked = 1;
            Directory.CreateDirectory(state.Settings.DownloadDir);
            foreach (var f in files)
            {
                var target = Path.Combine(state.Settings.DownloadDir, BoothParser.SafeFileName(f.Name));
                // 同名ファイルが既にあれば落とし直さない（版が変わればファイル名が変わるので新規だけ落ちる）。
                // もちふぃった～は 1GB 級なので、無駄な再ダウンロードを避ける。
                if (File.Exists(target))
                {
                    result.Downloaded.Add(new Downloaded(page.Title ?? itemId, f.Name, target));
                    continue;
                }
                await client.DownloadAsync(f.Href, target, ct);
                result.Downloaded.Add(new Downloaded(page.Title ?? itemId, f.Name, target));
            }
        }
        catch (NotLoggedInException)
        {
            result.LoggedIn = false;
            result.Errors.Add(new CheckError("BOOTH", "ログインしていない。--login で一度ログインしてください。"));
        }
        catch (Exception ex)
        {
            result.Errors.Add(new CheckError(itemId, ex.Message));
        }
        return result;
    }

    static bool MatchesPattern(TrackedItem item, FileEntry f)
    {
        if (string.IsNullOrWhiteSpace(item.FilePattern)) return true;
        // ワイルドカード（* ?）をそのまま使えるようにする
        var re = "^" + Regex.Escape(item.FilePattern).Replace(@"\*", ".*").Replace(@"\?", ".") + "$";
        return Regex.IsMatch(f.Name, re, RegexOptions.IgnoreCase);
    }

    public static async Task<CheckResult> CheckAllAsync(BoothClient client, AppState state, bool download, Action<string>? progress, CancellationToken ct = default)
    {
        var result = new CheckResult { DownloadDir = state.Settings.DownloadDir };
        if (state.Items.Count == 0) return result;

        Dictionary<string, (string? Title, List<FileEntry> Files)> library;
        try
        {
            library = await client.ScanLibraryAsync(progress, ct);
        }
        catch (NotLoggedInException)
        {
            result.LoggedIn = false;
            foreach (var it in state.Items.Values) it.Status = "not_logged_in";
            result.Errors.Add(new CheckError("ライブラリ", "BOOTH にログインしていない。--login で一度ログインしてください。"));
            return result;
        }
        catch (Exception ex)
        {
            result.Errors.Add(new CheckError("ライブラリ", ex.Message));
            return result;
        }

        foreach (var item in state.Items.Values)
        {
            try
            {
                string? title; List<FileEntry> files;
                if (item.OrderId != null)
                {
                    var o = await client.InspectOrderAsync(item.OrderId, ct);
                    title = o.Title; files = o.Files;
                    if (o.ItemId != null) item.ItemId = o.ItemId;
                    await Task.Delay(800, ct);
                }
                else if (item.ItemId != null && library.TryGetValue(item.ItemId, out var e))
                {
                    title = e.Title; files = e.Files;
                }
                else
                {
                    item.Status = "not_in_library";
                    item.LastChecked = DateTime.Now;
                    result.Checked++;
                    continue;
                }

                result.Checked++;
                item.LastChecked = DateTime.Now;
                item.Title = title ?? item.Title;
                item.Status = "ok";
                item.Error = null;
                item.RemoteFiles = files;

                var known = item.Files.Select(f => f.Id).ToHashSet();
                var fresh = files.Where(f => !known.Contains(f.Id) && MatchesPattern(item, f)).ToList();
                if (fresh.Count == 0) continue;

                if (download && state.Settings.AutoDownload)
                {
                    foreach (var f in fresh)
                    {
                        var target = UniquePath(Path.Combine(state.Settings.DownloadDir, BoothParser.SafeFileName(f.Name)));
                        progress?.Invoke($"保存中: {f.Name}");
                        await client.DownloadAsync(f.Href, target, ct);
                        result.Downloaded.Add(new Downloaded(item.Title ?? item.Key, f.Name, target));
                        item.Files.RemoveAll(x => x.Id == f.Id);
                        item.Files.Add(f);
                    }
                    foreach (var f in files.Where(f => !MatchesPattern(item, f) && !known.Contains(f.Id))) item.Files.Add(f);
                    item.LastDownloaded = DateTime.Now;
                }
                else
                {
                    result.Pending.Add(new Pending(item.Title ?? item.Key, fresh.Select(f => f.Name).ToList()));
                }
            }
            catch (Exception ex)
            {
                item.Status = "error";
                item.Error = ex.Message;
                result.Errors.Add(new CheckError(item.Title ?? item.Key, ex.Message));
            }
        }
        return result;
    }

    static string UniquePath(string path)
    {
        if (!File.Exists(path)) return path;
        var dir = Path.GetDirectoryName(path)!;
        var stem = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        for (int i = 1; ; i++)
        {
            var p = Path.Combine(dir, $"{stem} ({i}){ext}");
            if (!File.Exists(p)) return p;
        }
    }
}
