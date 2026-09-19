using Microsoft.Web.WebView2.WinForms;

namespace BoothAutoUpdater;

/// <summary>
/// BOOTH のログイン画面をそのまま表示する。入力は全部 BOOTH のページ上で行われ、
/// このアプリは何も読まない。ライブラリに到達したら閉じてよい。
/// </summary>
public class LoginForm : Form
{
    readonly WebView2 _wv = new() { Dock = DockStyle.Fill };
    readonly Label _hint = new()
    {
        Dock = DockStyle.Top, Height = 28, Padding = new Padding(8, 6, 8, 0),
        Text = "BOOTH にログインしてください。ライブラリが表示されたら、このウィンドウは閉じて構いません。"
    };

    public LoginForm()
    {
        Text = "BOOTH にログイン";
        Size = new Size(960, 760);
        StartPosition = FormStartPosition.CenterScreen;
        Controls.Add(_wv);
        Controls.Add(_hint);
        Load += async (_, _) =>
        {
            var env = await BoothClient.CreateEnvironmentAsync();
            await _wv.EnsureCoreWebView2Async(env);
            _wv.CoreWebView2.SourceChanged += (_, _) =>
            {
                var src = _wv.CoreWebView2.Source ?? "";
                if (src.StartsWith("https://accounts.booth.pm/library"))
                    _hint.Text = "ログイン済み。このウィンドウは閉じて構いません。";
            };
            _wv.CoreWebView2.Navigate("https://accounts.booth.pm/library");
        };
    }
}
