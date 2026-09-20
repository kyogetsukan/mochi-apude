using System.Text;
using System.Text.Json;

namespace MochiApude;

/// <summary>
/// 起動方法:
///   MochiApude.exe            設定ウィンドウ（商品の登録・削除、ログイン、手動チェック）
///   MochiApude.exe --check    登録商品を確認し新版を保存。結果 JSON を stdout に出して終了（Unity から呼ぶ）
///   MochiApude.exe --login    BOOTH のログイン画面を開く（初回のみ）
///   MochiApude.exe --add URL  商品 URL / 注文詳細 URL を登録して終了
/// </summary>
static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        // コンソール窓を持たないので Console.OutputEncoding は触らない（例外になる）。
        // 標準出力・標準エラーを UTF-8 の Writer に差し替えるだけにする。
        try
        {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true });
            Console.SetError(new StreamWriter(Console.OpenStandardError(), new UTF8Encoding(false)) { AutoFlush = true });
        }
        catch { /* 出力先が無い場合はそのまま */ }

        // 未処理例外でダイアログを出して止まらないようにする（Unity 側がタイムアウトで待つことになる）
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            try { Console.Error.WriteLine("UNHANDLED: " + e.ExceptionObject); } catch { }
            Environment.Exit(3);
        };
        Application.ThreadException += (_, e) =>
        {
            try { Console.Error.WriteLine("UNHANDLED: " + e.Exception); } catch { }
            Environment.Exit(3);
        };
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

        var mode = args.Length > 0 ? args[0] : "";
        switch (mode)
        {
            case "--check":
                return RunHeadless(async client =>
                {
                    var state = AppState.Load();
                    var r = await Updater.CheckAllAsync(client, state, download: true, progress: null);
                    state.Save();
                    Console.Out.WriteLine(JsonSerializer.Serialize(r, new JsonSerializerOptions { WriteIndented = true }));
                    return r.Errors.Count > 0 && r.Checked == 0 ? 2 : 0;
                });

            case "--check-item":
                {
                    if (args.Length < 2) { Console.Error.WriteLine("item id がない"); return 1; }
                    var itemId = args[1];
                    return RunHeadless(async client =>
                    {
                        var state = AppState.Load();
                        var r = await Updater.CheckSingleItemAsync(client, state, itemId);
                        Console.Out.WriteLine(JsonSerializer.Serialize(r, new JsonSerializerOptions { WriteIndented = true }));
                        return r.LoggedIn ? 0 : 2;
                    });
                }

            case "--add":
                {
                    if (args.Length < 2) { Console.Error.WriteLine("URL がない"); return 1; }
                    var state = AppState.Load();
                    var msg = Updater.AddItem(state, args[1]);
                    state.Save();
                    Console.Out.WriteLine(msg);
                    return 0;
                }

            case "--remove":
                {
                    if (args.Length < 2) { Console.Error.WriteLine("URL がない"); return 1; }
                    var state = AppState.Load();
                    Console.Out.WriteLine(Updater.RemoveItem(state, args[1]));
                    state.Save();
                    return 0;
                }

            case "--list":
                {
                    var state = AppState.Load();
                    Console.Out.WriteLine(JsonSerializer.Serialize(new
                    {
                        state.Settings.DownloadDir,
                        LoggedIn = Directory.Exists(AppState.ProfileDir),
                        Items = state.Items.Values.Select(i => new { i.Key, i.ItemId, i.OrderId, i.Title, i.Status, i.FilePattern, Link = i.Link, Files = i.Files.Select(f => f.Name).ToList() })
                    }, new JsonSerializerOptions { WriteIndented = true }));
                    return 0;
                }

            case "--login":
                Application.Run(new LoginForm());
                return 0;

            default:
                Application.Run(new SettingsForm());
                return 0;
        }
    }

    /// <summary>ウィンドウを出さずに WebView2 を回して処理し、終了コードを返す</summary>
    static int RunHeadless(Func<BoothClient, Task<int>> work)
    {
        int code = 1;
        var host = new HiddenHost();
        host.Load += async (_, _) =>
        {
            try
            {
                var client = await BoothClient.CreateAsync(host);
                code = await work(client);
            }
            catch (Exception ex)
            {
                Console.Out.WriteLine(JsonSerializer.Serialize(new CheckResult
                {
                    Errors = { new CheckError("全体", ex.Message) }
                }));
                code = 2;
            }
            finally
            {
                host.Close();
            }
        };
        Application.Run(host);
        return code;
    }
}

/// <summary>画面に出ない WebView2 の置き場</summary>
public class HiddenHost : Form
{
    public HiddenHost()
    {
        ShowInTaskbar = false;
        WindowState = FormWindowState.Minimized;
        FormBorderStyle = FormBorderStyle.None;
        Opacity = 0;
        Size = new Size(1, 1);
    }
}
