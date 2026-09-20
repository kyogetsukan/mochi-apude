namespace MochiApude;

/// <summary>商品の登録・削除、保存先、手動チェック。</summary>
public class SettingsForm : Form
{
    readonly AppState _state = AppState.Load();

    readonly TextBox _url = new() { PlaceholderText = "https://booth.pm/ja/items/1234567  または  https://accounts.booth.pm/orders/…", Dock = DockStyle.Fill };
    readonly Button _add = new() { Text = "追加", Width = 80, Dock = DockStyle.Right };
    readonly ListView _list = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HideSelection = false };
    readonly TextBox _pattern = new() { PlaceholderText = "ファイル名の絞り込み（例 MochiFitter*.zip）空なら全部", Dock = DockStyle.Fill };
    readonly Button _savePattern = new() { Text = "絞り込みを保存", Width = 120, Dock = DockStyle.Right };
    readonly Button _remove = new() { Text = "選択を削除", Width = 100 };
    readonly Button _login = new() { Text = "BOOTH にログイン", Width = 130 };
    readonly Button _check = new() { Text = "今すぐ更新チェック", Width = 140 };
    readonly Button _openDir = new() { Text = "保存先を開く", Width = 100 };
    readonly CheckBox _auto = new() { Text = "新版を自動で保存する", AutoSize = true, Checked = true };
    readonly TextBox _dir = new() { Dock = DockStyle.Fill };
    readonly TextBox _log = new() { Dock = DockStyle.Bottom, Height = 110, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };

    HiddenHost? _host;
    BoothClient? _client;

    public SettingsForm()
    {
        Text = "もちアプデ";
        Size = new Size(820, 620);
        MinimumSize = new Size(640, 480);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Yu Gothic UI", 9.5f);

        _list.Columns.Add("商品", 300);
        _list.Columns.Add("状態", 100);
        _list.Columns.Add("手元のファイル", 220);
        _list.Columns.Add("最終確認", 110);
        _list.Columns.Add("絞り込み", 120);

        var top = new Panel { Dock = DockStyle.Top, Height = 32, Padding = new Padding(8, 4, 8, 4) };
        top.Controls.Add(_url); top.Controls.Add(_add);

        var pat = new Panel { Dock = DockStyle.Top, Height = 32, Padding = new Padding(8, 4, 8, 4) };
        pat.Controls.Add(_pattern); pat.Controls.Add(_savePattern);

        var dirRow = new Panel { Dock = DockStyle.Top, Height = 32, Padding = new Padding(8, 4, 8, 4) };
        var dirLabel = new Label { Text = "保存先", Dock = DockStyle.Left, Width = 50, TextAlign = ContentAlignment.MiddleLeft };
        dirRow.Controls.Add(_dir); dirRow.Controls.Add(dirLabel); dirRow.Controls.Add(_openDir);
        _openDir.Dock = DockStyle.Right;

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(6, 4, 6, 4) };
        buttons.Controls.AddRange(new Control[] { _check, _login, _remove, _auto });

        Controls.Add(_list);
        Controls.Add(_log);
        Controls.Add(buttons);
        Controls.Add(dirRow);
        Controls.Add(pat);
        Controls.Add(top);

        _add.Click += (_, _) => { Log(Updater.AddItem(_state, _url.Text.Trim())); _url.Clear(); Persist(); Render(); };
        _remove.Click += (_, _) => { foreach (ListViewItem li in _list.SelectedItems) _state.Items.Remove((string)li.Tag!); Persist(); Render(); };
        _login.Click += (_, _) => { using var f = new LoginForm(); f.ShowDialog(this); };
        _openDir.Click += (_, _) => { Directory.CreateDirectory(_state.Settings.DownloadDir); System.Diagnostics.Process.Start("explorer.exe", _state.Settings.DownloadDir); };
        _auto.CheckedChanged += (_, _) => { _state.Settings.AutoDownload = _auto.Checked; Persist(); };
        _dir.Leave += (_, _) => { if (!string.IsNullOrWhiteSpace(_dir.Text)) { _state.Settings.DownloadDir = _dir.Text.Trim(); Persist(); } };
        _list.SelectedIndexChanged += (_, _) =>
        {
            var it = Selected();
            _pattern.Text = it?.FilePattern ?? "";
        };
        _savePattern.Click += (_, _) =>
        {
            var it = Selected();
            if (it == null) { Log("商品を選んでから。"); return; }
            it.FilePattern = string.IsNullOrWhiteSpace(_pattern.Text) ? null : _pattern.Text.Trim();
            Persist(); Render();
        };
        _check.Click += async (_, _) => await RunCheckAsync();

        _auto.Checked = _state.Settings.AutoDownload;
        _dir.Text = _state.Settings.DownloadDir;
        Render();
        Log("商品 URL を追加して「今すぐ更新チェック」。初回は「BOOTH にログイン」が必要。");
    }

    TrackedItem? Selected() => _list.SelectedItems.Count > 0 ? _state.Items.GetValueOrDefault((string)_list.SelectedItems[0].Tag!) : null;

    void Persist() { try { _state.Save(); } catch (Exception ex) { Log("保存失敗: " + ex.Message); } }

    void Render()
    {
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var it in _state.Items.Values)
        {
            var li = new ListViewItem(it.Title ?? it.Link) { Tag = it.Key };
            li.SubItems.Add(it.Status switch
            {
                "ok" => "追跡中", "new" => "未チェック", "not_in_library" => "ライブラリに無い",
                "not_logged_in" => "未ログイン", "error" => "エラー", var s => s
            });
            li.SubItems.Add(string.Join(", ", it.Files.Select(f => f.Name)));
            li.SubItems.Add(it.LastChecked?.ToString("MM/dd HH:mm") ?? "—");
            li.SubItems.Add(it.FilePattern ?? "");
            if (it.Error != null) li.ToolTipText = it.Error;
            _list.Items.Add(li);
        }
        _list.EndUpdate();
    }

    void Log(string s)
    {
        _log.AppendText(s + Environment.NewLine);
    }

    async Task RunCheckAsync()
    {
        _check.Enabled = false;
        try
        {
            if (_client == null)
            {
                _host = new HiddenHost();
                _host.Show();
                _client = await BoothClient.CreateAsync(_host);
            }
            Log("ライブラリを確認中…");
            var r = await Updater.CheckAllAsync(_client, _state, download: true,
                progress: s => { _log.Text = _log.Text.TrimEnd() + Environment.NewLine + "  " + s.Replace("https://accounts.booth.pm/", "") + Environment.NewLine; });
            Persist();
            Log($"確認 {r.Checked} 件");
            foreach (var d in r.Downloaded) Log($"保存: {d.Item} / {d.File}");
            foreach (var p in r.Pending) Log($"新版あり（未保存）: {p.Item} / {string.Join(", ", p.Files)}");
            foreach (var e in r.Errors) Log($"失敗: {e.Item} — {e.Error}");
            if (r.Downloaded.Count == 0 && r.Pending.Count == 0 && r.Errors.Count == 0) Log("新しいファイルは無い。");
        }
        catch (Exception ex) { Log("失敗: " + ex.Message); }
        finally { _check.Enabled = true; Render(); }
    }
}
