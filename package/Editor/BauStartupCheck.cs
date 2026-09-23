using UnityEditor;
using UnityEngine;

namespace Kyogetsukan.MochiApude
{
    /// <summary>
    /// Unity を開いた時（エディタ起動時）に一度だけ、勝手に BOOTH を確認しておく。
    /// スクリプトの再コンパイルのたびに走らないよう、SessionState で
    /// 「このエディタセッション中にもう済んだか」を覚える
    /// （SessionState はドメインリロードをまたいで残り、エディタを閉じて開き直すとリセットされる）。
    /// 取り込みまではせず、Console に一言出すだけ。パネルを開いた時は、ここで分かった結果を
    /// そのまま表示するだけにして、BOOTH にはもう一度聞きに行かない。
    /// </summary>
    [InitializeOnLoad]
    public static class BauStartupCheck
    {
        const string SessionKey = "Kyogetsukan.MochiApude.StartupChecked";

        /// <summary>最後に分かった結果の一言。何もなければ空文字のまま。パネルが開いた時の表示用。</summary>
        public static string LastMessage { get; private set; } = "";

        static BauStartupCheck()
        {
            if (!BauExe.IsWindows) return;
            if (SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);
            // 起動直後のごたごた（アセット読み込み中など）を避けて少し遅らせる
            EditorApplication.delayCall += Run;
        }

        static void Run()
        {
            BauExe.CheckItem(BauProduct.ItemId, (r, err) =>
            {
                if (r != null && !string.IsNullOrEmpty(r.DownloadDir)) BauImporter.DownloadDir = r.DownloadDir;
                if (r == null)
                {
                    Debug.Log("[もちアプデ] 起動時確認: 失敗 — " + (err ?? "不明"));
                    return;
                }
                if (!r.LoggedIn)
                {
                    Debug.Log("[もちアプデ] 起動時確認: BOOTH にログインしていません。");
                    return;
                }

                var s = BauSettings.Load();
                // 保存フォルダは常に最新1版だけにする（古い版が残ると両方取り込む事故になる）
                var removed = BauImporter.KeepOnlyLatest(BauProduct.FilePrefix, s);
                foreach (var name in removed) Debug.Log("[もちアプデ] 古い版を old へ退避: " + name);

                var found = BauImporter.FindByPrefix(BauProduct.FilePrefix, includeImported: false, s);
                if (found.Count > 1) found = found.GetRange(0, 1); // 念のため最新1つに絞る
                if (found.Count == 0) return; // 何もなければ黙っておく

                LastMessage = "更新来てます: " + string.Join(", ", found.ConvertAll(f => f.fileName))
                    + "（Tools → 境月館 → もちアプデ、または境月箱 から取り込めます）";
                Debug.Log("[もちアプデ] " + LastMessage);
            });
        }
    }
}
