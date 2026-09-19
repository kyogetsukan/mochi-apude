using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace Kyogetsukan.BoothAutoUpdater
{
    /// <summary>exe（BoothAutoUpdater.exe）の呼び出しと結果の受け取り</summary>
    public static class BauExe
    {
        public const string PackageName = "jp.kyogetsukan.booth-auto-updater";
        const string ExeName = "BoothAutoUpdater.exe";

        /// <summary>パッケージに同梱されている exe 一式の入ったフォルダ（Tools~/app/）</summary>
        public static string BundledDir
        {
            get
            {
                var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(BauExe).Assembly);
                var root = info != null ? info.resolvedPath : Path.GetFullPath("Packages/" + PackageName);
                return Path.Combine(root, "Tools~", "app");
            }
        }

        /// <summary>同梱 exe（Tools~/app/BoothAutoUpdater.exe）</summary>
        public static string BundledExePath => Path.Combine(BundledDir, ExeName);

        /// <summary>
        /// 実際に起動する exe。Tools~ から直接は起動せず、%LOCALAPPDATA%\BoothAutoUpdater\bin\ に
        /// フォルダごとコピーしてそこから動かす。パスの「~」を .NET のホストが誤解する事故と、
        /// PackageCache 側の読み取り専用・差し替えの影響を避けるため。
        /// </summary>
        public static string DeployDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BoothAutoUpdater", "bin");

        public static string ExePath => Path.Combine(DeployDir, ExeName);

        public static bool IsWindows =>
            Application.platform == RuntimePlatform.WindowsEditor;

        /// <summary>同梱 exe 一式を起動場所へ写す（無い時、または本体が変わった時だけ）</summary>
        static bool EnsureExeDeployed(out string error)
        {
            error = null;
            if (!IsWindows) { error = "Windows 以外では BOOTH の確認はできない"; return false; }
            var srcExe = BundledExePath;
            var dstExe = ExePath;
            try
            {
                if (!File.Exists(srcExe))
                {
                    if (File.Exists(dstExe)) return true; // 以前コピーしたものが残っていればそれで動かす
                    error = "BoothAutoUpdater.exe が見つからない: " + srcExe;
                    return false;
                }
                var s = new FileInfo(srcExe);
                var d = new FileInfo(dstExe);
                if (!d.Exists || d.Length != s.Length || d.LastWriteTimeUtc != s.LastWriteTimeUtc)
                {
                    // フォルダごとコピー（dll・runtimes 一式が要る）
                    Directory.CreateDirectory(DeployDir);
                    foreach (var f in Directory.GetFiles(BundledDir, "*", SearchOption.AllDirectories))
                    {
                        var rel = f.Substring(BundledDir.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                        var to = Path.Combine(DeployDir, rel);
                        Directory.CreateDirectory(Path.GetDirectoryName(to));
                        File.Copy(f, to, true);
                    }
                    File.SetLastWriteTimeUtc(dstExe, s.LastWriteTimeUtc);
                    UnityEngine.Debug.Log("[BOOTH Auto Updater] exe 一式を配置: " + DeployDir);
                }
                return true;
            }
            catch (Exception e)
            {
                if (File.Exists(dstExe)) return true; // 起動中で上書きできない等。既存で動かす
                error = "exe の配置に失敗: " + e.Message;
                return false;
            }
        }

        public static bool ExeExists => IsWindows && (File.Exists(BundledExePath) || File.Exists(ExePath));

        public static string DefaultDownloadDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "VRC_Packages");

        // ---- exe の出力（JsonUtility 用にフィールド名を exe 側 PascalCase に合わせる）----
        [Serializable] public class CheckResult
        {
            public int Checked;
            public bool LoggedIn = true;
            public List<Downloaded> Downloaded = new List<Downloaded>();
            public List<Pending> Pending = new List<Pending>();
            public List<CheckError> Errors = new List<CheckError>();
            public string DownloadDir;
        }
        [Serializable] public class Downloaded { public string Item; public string File; public string Path; }
        [Serializable] public class Pending { public string Item; public List<string> Files; }
        [Serializable] public class CheckError { public string Item; public string Error; }

        [Serializable] public class ListResult
        {
            public string DownloadDir;
            public bool LoggedIn;
            public List<ListItem> Items = new List<ListItem>();
        }
        [Serializable] public class ListItem
        {
            public string Key; public string ItemId; public string OrderId; public string Title;
            public string Status; public string FilePattern; public string Link; public List<string> Files;
        }

        /// <summary>exe を引数付きで実行し、標準出力を返す。UI をブロックしないよう別スレッドで待ち、完了はメインスレッドで通知。</summary>
        public static void Run(string args, Action<int, string, string> onDone, int timeoutMs = 20 * 60 * 1000)
        {
            if (!EnsureExeDeployed(out var deployError))
            {
                onDone?.Invoke(-1, "", deployError);
                return;
            }
            var psi = new ProcessStartInfo
            {
                FileName = ExePath,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            var thread = new Thread(() =>
            {
                int code = -1; string out_ = "", err = "";
                try
                {
                    using (var p = Process.Start(psi))
                    {
                        var so = p.StandardOutput.ReadToEndAsync();
                        var se = p.StandardError.ReadToEndAsync();
                        if (!p.WaitForExit(timeoutMs)) { try { p.Kill(); } catch { } err = "タイムアウト"; }
                        else { code = p.ExitCode; }
                        out_ = so.Result; err += se.Result;
                    }
                }
                catch (Exception e) { err = e.Message; }
                BauMainThread.Enqueue(() => onDone?.Invoke(code, out_, err));
            }) { IsBackground = true, Name = "BoothAutoUpdater" };
            thread.Start();
        }

        /// <summary>ウィンドウを出す系（--login / 設定画面）は待たずに起動する</summary>
        public static void Launch(string args)
        {
            if (!EnsureExeDeployed(out var deployError)) { EditorUtility.DisplayDialog("BOOTH Auto Updater", deployError, "OK"); return; }
            Process.Start(new ProcessStartInfo { FileName = ExePath, Arguments = args, UseShellExecute = true });
        }

        public static void Check(Action<CheckResult, string> onDone)
        {
            Run("--check", (code, out_, err) =>
            {
                CheckResult r = null;
                try { if (!string.IsNullOrWhiteSpace(out_)) r = JsonUtility.FromJson<CheckResult>(out_); } catch { }
                onDone?.Invoke(r, string.IsNullOrEmpty(err) ? (r == null ? "exe の出力を読めない (code " + code + ")" : null) : err);
            });
        }

        public static void List(Action<ListResult, string> onDone)
        {
            Run("--list", (code, out_, err) =>
            {
                ListResult r = null;
                try { if (!string.IsNullOrWhiteSpace(out_)) r = JsonUtility.FromJson<ListResult>(out_); } catch { }
                onDone?.Invoke(r, string.IsNullOrEmpty(err) ? (r == null ? "exe の出力を読めない" : null) : err);
            }, 30 * 1000);
        }

        public static void Add(string url, Action<string> onDone) =>
            Run("--add \"" + url.Replace("\"", "") + "\"", (c, o, e) => onDone?.Invoke(string.IsNullOrEmpty(e) ? o.Trim() : e), 30 * 1000);

        public static void Remove(string urlOrKey, Action<string> onDone) =>
            Run("--remove \"" + urlOrKey.Replace("\"", "") + "\"", (c, o, e) => onDone?.Invoke(string.IsNullOrEmpty(e) ? o.Trim() : e), 30 * 1000);

        /// <summary>単一商品だけを商品ページ→注文詳細経由で確認・保存する（もちふぃった～専用版が使う）</summary>
        public static void CheckItem(string itemId, Action<CheckResult, string> onDone)
        {
            Run("--check-item " + itemId, (code, out_, err) =>
            {
                CheckResult r = null;
                try { if (!string.IsNullOrWhiteSpace(out_)) r = JsonUtility.FromJson<CheckResult>(out_); } catch { }
                onDone?.Invoke(r, string.IsNullOrEmpty(err) ? (r == null ? "exe の出力を読めない (code " + code + ")" : null) : err);
            });
        }
    }

    /// <summary>別スレッドからメインスレッドへ処理を渡す</summary>
    [InitializeOnLoad]
    public static class BauMainThread
    {
        static readonly Queue<Action> Queue = new Queue<Action>();
        static BauMainThread() { EditorApplication.update += Pump; }
        public static void Enqueue(Action a) { lock (Queue) Queue.Enqueue(a); }
        static void Pump()
        {
            Action a = null;
            lock (Queue) if (Queue.Count > 0) a = Queue.Dequeue();
            try { a?.Invoke(); } catch (Exception e) { UnityEngine.Debug.LogException(e); }
        }
    }
}
