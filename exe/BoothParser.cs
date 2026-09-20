using System.Net;
using System.Text.RegularExpressions;

namespace MochiApude;

public record Downloadable(string? ItemId, string? Title, string Id, string Href, string Name);

/// <summary>
/// BOOTH のライブラリ / 注文詳細 HTML から、ダウンロード可能ファイルを抜き出す。
/// 依存している目印: js-download-button の data-href、直前の <div class="text-14">ファイル名</div>、
/// 直前の items/ID リンク、直前の太字タイトル。BOOTH の HTML が変わったらここを直す。
/// </summary>
public static class BoothParser
{
    static readonly Regex ItemUrl = new(@"booth\.pm/(?:[a-z-]+/)?items/(\d+)", RegexOptions.IgnoreCase);
    static readonly Regex OrderUrl = new(@"accounts\.booth\.pm/orders/(\d+)", RegexOptions.IgnoreCase);

    // 旧マークアップ: <div class="text-14">ファイル名</div>
    static readonly Regex NameRe = new(@"<div class=""text-14"">([^<]+)</div>");
    // 新マークアップ(2026-): <div class="min-w-0 ... text-[#505c6b] ..."><b>ファイル名</b></div>
    static readonly Regex NameReNew = new(@"text-\[#505c6b\][^""]*""><b>([^<]+)</b>");
    static readonly Regex ItemLinkRe = new(@"href=""https://booth\.pm/(?:[a-z-]+/)?items/(\d+)""");
    static readonly Regex TitleRe = new(@"<div class=""text-text-default font-bold text-16[^""]*"">([^<]+)</div>");
    static readonly Regex ButtonRe = new(@"class=""js-download-button""[^>]*data-href=""(https://booth\.pm/downloadables/(\d+))""");

    public static string? ParseItemId(string url) => ItemUrl.Match(url) is { Success: true } m ? m.Groups[1].Value : null;
    public static string? ParseOrderId(string url) => OrderUrl.Match(url) is { Success: true } m ? m.Groups[1].Value : null;

    public static List<Downloadable> ParseDownloadables(string html)
    {
        var marks = new List<(int idx, char kind, string value)>();
        foreach (Match m in NameRe.Matches(html)) marks.Add((m.Index, 'n', WebUtility.HtmlDecode(m.Groups[1].Value.Trim())));
        foreach (Match m in NameReNew.Matches(html)) marks.Add((m.Index, 'n', WebUtility.HtmlDecode(m.Groups[1].Value.Trim())));
        foreach (Match m in ItemLinkRe.Matches(html)) marks.Add((m.Index, 'i', m.Groups[1].Value));
        foreach (Match m in TitleRe.Matches(html)) marks.Add((m.Index, 't', WebUtility.HtmlDecode(m.Groups[1].Value.Trim())));
        marks.Sort((a, b) => a.idx.CompareTo(b.idx));

        var result = new List<Downloadable>();
        foreach (Match b in ButtonRe.Matches(html))
        {
            string? name = null, itemId = null, title = null;
            for (int i = marks.Count - 1; i >= 0; i--)
            {
                var mk = marks[i];
                if (mk.idx >= b.Index) continue;
                if (mk.kind == 'n' && name == null) name = mk.value;
                if (mk.kind == 'i' && itemId == null) itemId = mk.value;
                if (mk.kind == 't' && title == null) title = mk.value;
                if (name != null && itemId != null && title != null) break;
            }
            var id = b.Groups[2].Value;
            result.Add(new Downloadable(itemId, title, id, b.Groups[1].Value, name ?? $"downloadable_{id}"));
        }
        return result;
    }

    public static string SafeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return name.Length > 180 ? name[..180] : name;
    }
}
