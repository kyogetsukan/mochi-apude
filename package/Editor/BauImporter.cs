using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Kyogetsukan.MochiApude
{
    /// <summary>保存フォルダの zip / unitypackage を見つけて、このプロジェクトに取り込む</summary>
    public static class BauImporter
    {
        public class Candidate
        {
            public string path;
            public string fileName;
            public long size;
            public DateTime modified;
            public bool selected = true;
        }

        public static string DownloadDir { get; set; } = BauExe.DefaultDownloadDir;

        /// <summary>まだこのプロジェクトに入れておらず、見送ってもいないファイル</summary>
        public static List<Candidate> FindNew(BauSettings s)
        {
            var list = new List<Candidate>();
            if (!Directory.Exists(DownloadDir)) return list;
            foreach (var p in Directory.GetFiles(DownloadDir))
            {
                var ext = Path.GetExtension(p).ToLowerInvariant();
                if (ext != ".zip" && ext != ".unitypackage") continue;
                var fi = new FileInfo(p);
                if (s.IsImported(fi.Name, fi.Length) || s.IsSkipped(fi.Name)) continue;
                list.Add(new Candidate { path = p, fileName = fi.Name, size = fi.Length, modified = fi.LastWriteTime });
            }
            return list.OrderByDescending(c => c.modified).ToList();
        }

        /// <summary>保存フォルダから名前が prefix で始まる zip/unitypackage を探す。includeImported=true なら取り込み済みも含める（入れ直し用）</summary>
        public static List<Candidate> FindByPrefix(string prefix, bool includeImported, BauSettings s)
        {
            var list = new List<Candidate>();
            if (!Directory.Exists(DownloadDir)) return list;
            foreach (var p in Directory.GetFiles(DownloadDir))
            {
                var ext = Path.GetExtension(p).ToLowerInvariant();
                if (ext != ".zip" && ext != ".unitypackage") continue;
                var fi = new FileInfo(p);
                if (!fi.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                if (!includeImported && (s.IsImported(fi.Name, fi.Length) || s.IsSkipped(fi.Name))) continue;
                list.Add(new Candidate { path = p, fileName = fi.Name, size = fi.Length, modified = fi.LastWriteTime });
            }
            return list.OrderByDescending(c => c.modified).ToList();
        }

        /// <summary>
        /// 保存フォルダにある prefix 始まりの zip/unitypackage のうち、更新日時が最新の1つだけを残し、古い版は削除する。
        /// 削除したファイルの取り込み済み・見送り記録も消す。戻り値は削除したファイル名。
        /// 版が並ぶと「両方入れる」ことになりかねないので、常に最新1つに揃える。
        /// </summary>
        public static List<string> KeepOnlyLatest(string prefix, BauSettings s)
        {
            var removed = new List<string>();
            if (!Directory.Exists(DownloadDir)) return removed;
            var files = new List<FileInfo>();
            foreach (var p in Directory.GetFiles(DownloadDir))
            {
                var ext = Path.GetExtension(p).ToLowerInvariant();
                if (ext != ".zip" && ext != ".unitypackage") continue;
                var fi = new FileInfo(p);
                if (!fi.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                files.Add(fi);
            }
            if (files.Count <= 1) return removed;
            var latest = files.OrderByDescending(f => f.LastWriteTime).First();
            foreach (var fi in files)
            {
                if (fi.FullName == latest.FullName) continue;
                try
                {
                    fi.Delete();
                    s.imported.RemoveAll(r => r.fileName == fi.Name);
                    s.skipped.Remove(fi.Name);
                    removed.Add(fi.Name);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[もちアプデ] 古い版を消せませんでした: " + fi.Name + " — " + e.Message);
                }
            }
            if (removed.Count > 0) s.Save();
            return removed;
        }

        /// <summary>取り込み済み記録を消してから入れ直す（入れ直しボタン用）</summary>
        public static void Reimport(IEnumerable<Candidate> candidates, BauSettings s)
        {
            foreach (var c in candidates) s.imported.RemoveAll(r => r.fileName == c.fileName);
            s.Save();
            Import(candidates, s);
        }

        /// <summary>選ばれたものを順に取り込み、選ばれなかったものは見送りとして記録</summary>
        public static void Import(IEnumerable<Candidate> candidates, BauSettings s)
        {
            var queue = new Queue<string>();
            var owners = new Dictionary<string, Candidate>();
            foreach (var c in candidates)
            {
                if (!c.selected) { if (!s.skipped.Contains(c.fileName)) s.skipped.Add(c.fileName); continue; }
                foreach (var pkg in ExtractUnityPackages(c.path))
                {
                    queue.Enqueue(pkg);
                    owners[pkg] = c;
                }
                if (!owners.ContainsValue(c))
                    Debug.LogWarning("[もちアプデ] unitypackage が見つからないので見送り: " + c.fileName);
            }
            s.Save();
            if (queue.Count == 0) return;
            ImportNext(queue, owners, s);
        }

        static void ImportNext(Queue<string> queue, Dictionary<string, Candidate> owners, BauSettings s)
        {
            if (queue.Count == 0)
            {
                AssetDatabase.Refresh();
                Debug.Log("[もちアプデ] 取り込み完了");
                return;
            }
            var pkg = queue.Dequeue();
            var owner = owners[pkg];
            Debug.Log("[もちアプデ] 取り込み: " + Path.GetFileName(pkg) + "  ← " + owner.fileName);

            AssetDatabase.ImportPackageCallback done = null;
            AssetDatabase.ImportPackageFailedCallback failed = null;
            // 取り込みでスクリプトが再コンパイルされると、この後のコールバックが消えることがある。
            // 記録は先に付けておき、失敗した時だけ取り消す。
            s.MarkImported(owner.fileName, owner.size);
            s.Save();
            done = name =>
            {
                AssetDatabase.importPackageCompleted -= done;
                AssetDatabase.importPackageFailed -= failed;
                ImportNext(queue, owners, s);
            };
            failed = (name, msg) =>
            {
                AssetDatabase.importPackageCompleted -= done;
                AssetDatabase.importPackageFailed -= failed;
                s.imported.RemoveAll(r => r.fileName == owner.fileName);
                s.Save();
                Debug.LogError("[もちアプデ] 取り込み失敗: " + name + " — " + msg);
                ImportNext(queue, owners, s);
            };
            AssetDatabase.importPackageCompleted += done;
            AssetDatabase.importPackageFailed += failed;
            AssetDatabase.ImportPackage(pkg, false);
        }

        /// <summary>zip なら Temp に展開して中の unitypackage のパスを返す。unitypackage ならそのまま返す。</summary>
        static IEnumerable<string> ExtractUnityPackages(string path)
        {
            if (path.EndsWith(".unitypackage", StringComparison.OrdinalIgnoreCase))
                return new[] { path };

            var dest = Path.Combine(Path.GetFullPath("Temp"), "MochiApude", Path.GetFileNameWithoutExtension(path));
            try
            {
                if (Directory.Exists(dest)) Directory.Delete(dest, true);
                Directory.CreateDirectory(dest);
                // 日本語ファイル名の zip に備えて Shift_JIS も試す
                try { ZipFile.ExtractToDirectory(path, dest); }
                catch
                {
                    Directory.Delete(dest, true); Directory.CreateDirectory(dest);
                    using (var z = ZipFile.Open(path, ZipArchiveMode.Read, System.Text.Encoding.GetEncoding(932)))
                        z.ExtractToDirectory(dest);
                }
                return Directory.GetFiles(dest, "*.unitypackage", SearchOption.AllDirectories)
                                .OrderBy(p => p.Length) // ネストが浅いものを先に
                                .ToArray();
            }
            catch (Exception e)
            {
                Debug.LogError("[もちアプデ] zip の展開に失敗: " + path + " — " + e.Message);
                return Array.Empty<string>();
            }
        }
    }
}
