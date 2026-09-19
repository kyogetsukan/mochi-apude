using System.Text.Json;
using System.Text.Json.Serialization;

namespace BoothAutoUpdater;

public class FileEntry
{
    public string Id { get; set; } = "";
    public string Href { get; set; } = "";
    public string Name { get; set; } = "";
}

public class TrackedItem
{
    public string? ItemId { get; set; }
    public string? OrderId { get; set; }
    public string? Title { get; set; }
    /// <summary>落とすファイル名の絞り込み（例: MochiFitter*.zip）。空なら全部</summary>
    public string? FilePattern { get; set; }
    public string Status { get; set; } = "new";   // new / ok / not_in_library / not_logged_in / error
    public string? Error { get; set; }
    public List<FileEntry> Files { get; set; } = new();        // 手元に保存済みとみなす一覧
    public List<FileEntry> RemoteFiles { get; set; } = new();  // 最終確認時に BOOTH 側にあった一覧
    public DateTime? LastChecked { get; set; }
    public DateTime? LastDownloaded { get; set; }

    [JsonIgnore]
    public string Key => OrderId != null ? $"order:{OrderId}" : $"item:{ItemId}";

    [JsonIgnore]
    public string Link => OrderId != null
        ? $"https://accounts.booth.pm/orders/{OrderId}"
        : $"https://booth.pm/ja/items/{ItemId}";
}

public class Settings
{
    public bool AutoDownload { get; set; } = true;
    public bool CheckOnVccStart { get; set; } = true;
    public string DownloadDir { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "VRC_Packages");
    /// <summary>起動を監視するプロセス名（拡張子なし）</summary>
    public List<string> WatchProcesses { get; set; } = new() { "CreatorCompanion", "ALCOM", "vrc-get-gui" };
}

public class AppState
{
    public Dictionary<string, TrackedItem> Items { get; set; } = new();
    public Settings Settings { get; set; } = new();

    static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };

    public static string Dir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BoothAutoUpdater");
    public static string FilePath => Path.Combine(Dir, "state.json");
    public static string ProfileDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BoothAutoUpdater", "profile");

    public static AppState Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppState>(File.ReadAllText(FilePath), Opts) ?? new AppState();
        }
        catch { /* 壊れていたら作り直す */ }
        return new AppState();
    }

    public void Save()
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Opts));
    }
}
