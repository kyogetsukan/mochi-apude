using UnityEditor;
using UnityEngine;

namespace Kyogetsukan.MochiApude
{
    /// <summary>
    /// もちアプデの中身（GUI＋処理）。単体ウィンドウでも境月箱の中でも、同じものを描く。
    /// 状態（ログ・処理中）はこのインスタンスが持つ。
    /// </summary>
    public class MochiApudePanel
    {
        const string ItemId = "7657840";
        const string ItemUrl = "https://booth.pm/ja/items/7657840";
        const string FilePrefix = "MochiFitter";

        string _log = "";
        bool _busy;

        // 再描画の依頼先。ホスト（ウィンドウ / 箱）が差し替える。
        public System.Action Repaint = () => UnityEditorInternal.InternalEditorUtility.RepaintAllViews();

        public void Draw()
        {
            EditorGUILayout.LabelField("もちふぃった～ 更新チェック", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("購入済みのもちふぃった～に新しい版が出ていれば取得して取り込みます。", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField("※ギフトで受け取ったものはこの方式では取得できません。", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(8);

            if (!BauExe.IsWindows)
            {
                EditorGUILayout.HelpBox("Windows のみ対応です。", MessageType.Warning);
                return;
            }

            using (new EditorGUI.DisabledScope(_busy))
            {
                if (GUILayout.Button("更新をチェックして取り込む", GUILayout.Height(30))) CheckAndImport();
                EditorGUILayout.Space(4);
                if (GUILayout.Button("入れ直す（手元の版をもう一度取り込む）", GUILayout.Height(26))) Reimport();
                EditorGUILayout.Space(4);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("BOOTH にログイン")) BauExe.Launch("--login");
                    if (GUILayout.Button("商品ページを開く")) Application.OpenURL(ItemUrl);
                    if (GUILayout.Button("保存フォルダ")) { System.IO.Directory.CreateDirectory(BauImporter.DownloadDir); EditorUtility.RevealInFinder(BauImporter.DownloadDir); }
                }
            }

            if (_busy) EditorGUILayout.LabelField("処理中…", EditorStyles.miniLabel);
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("ログ", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(_log, EditorStyles.helpBox, GUILayout.MinHeight(80), GUILayout.ExpandHeight(true));
        }

        void Log(string s) { _log += (_log.Length > 0 ? "\n" : "") + s; Repaint?.Invoke(); }

        void CheckAndImport()
        {
            _busy = true; _log = "";
            Log("BOOTH を確認中…（少し時間がかかります）");
            BauExe.CheckItem(ItemId, (r, err) =>
            {
                _busy = false;
                if (r != null && !string.IsNullOrEmpty(r.DownloadDir)) BauImporter.DownloadDir = r.DownloadDir;
                if (r == null) { Log("失敗: " + (err ?? "不明")); return; }
                if (!r.LoggedIn) { Log("BOOTH にログインしていません。「BOOTH にログイン」を押してください。"); return; }
                foreach (var d in r.Downloaded) Log("保存: " + d.File);
                foreach (var e in r.Errors) Log("注意: " + e.Item + " — " + e.Error);

                var s = BauSettings.Load();
                // 保存フォルダは常に最新1版だけにする（古い版が残ると両方取り込む事故になる）
                var removed = BauImporter.KeepOnlyLatest(FilePrefix, s);
                foreach (var name in removed) Log("古い版を削除: " + name);
                var found = BauImporter.FindByPrefix(FilePrefix, includeImported: false, s);
                if (found.Count > 1) found = found.GetRange(0, 1); // 念のため最新1つに絞る（FindByPrefix は新しい順）
                if (found.Count == 0) { Log("このプロジェクトに未取り込みの新しい版はありません。"); return; }
                Log("取り込み対象: " + string.Join(", ", found.ConvertAll(f => f.fileName)));
                if (s.confirmBeforeImport) BauConfirmWindow.Open(found, s);
                else BauImporter.Import(found, s);
            });
        }

        void Reimport()
        {
            var s = BauSettings.Load();
            var removed = BauImporter.KeepOnlyLatest(FilePrefix, s);
            foreach (var name in removed) Log("古い版を削除: " + name);
            var found = BauImporter.FindByPrefix(FilePrefix, includeImported: true, s);
            if (found.Count > 1) found = found.GetRange(0, 1); // 入れ直すのは最新1つだけ
            if (found.Count == 0)
            {
                Log("保存フォルダに " + FilePrefix + " のファイルがありません。先に「更新をチェックして取り込む」を実行してください。");
                return;
            }
            var names = string.Join("\n", found.ConvertAll(f => f.fileName));
            if (!EditorUtility.DisplayDialog("もちふぃった～ 入れ直し",
                    "次のファイルをこのプロジェクトに入れ直します。\n\n" + names, "入れ直す", "やめる"))
                return;
            Log("入れ直し: " + string.Join(", ", found.ConvertAll(f => f.fileName)));
            BauImporter.Reimport(found, s);
        }
    }
}
