using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Kyogetsukan.BoothAutoUpdater
{
    /// <summary>
    /// もちふぃった～専用の簡易版。商品は固定、ライブラリ走査なし。
    /// 商品ページ→注文詳細だけを見て取得するので速い。ギフト受取品は対象外（商品ページに注文が出ないため）。
    /// </summary>
    public class BauMochi : EditorWindow
    {
        // もちふぃった～（Nine Gates）
        const string ItemId = "7657840";
        const string ItemUrl = "https://booth.pm/ja/items/7657840";
        const string Title = "もちふぃった～";
        const string FilePrefix = "MochiFitter"; // 保存ファイル名の頭（MochiFitter68r.zip 等）

        const string MenuRoot = "Tools/境月館/もちアプデ/";

        string _log = "";
        bool _busy;

        [MenuItem(MenuRoot + "ウィンドウを開く", false, 0)]
        public static void Open()
        {
            var w = GetWindow<BauMochi>("もちアプデ");
            w.minSize = new Vector2(460, 300);
        }

        void OnGUI()
        {
            EditorGUILayout.Space(8);
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
            EditorGUILayout.SelectableLabel(_log, EditorStyles.helpBox, GUILayout.ExpandHeight(true));
        }

        void Log(string s) { _log += (_log.Length > 0 ? "\n" : "") + s; Repaint(); }

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
                var found = BauImporter.FindByPrefix(FilePrefix, includeImported: false, s);
                if (found.Count == 0) { Log("このプロジェクトに未取り込みの新しい版はありません。"); return; }
                Log("取り込み対象: " + string.Join(", ", found.ConvertAll(f => f.fileName)));
                if (s.confirmBeforeImport) BauConfirmWindow.Open(found, s);
                else BauImporter.Import(found, s);
            });
        }

        void Reimport()
        {
            var s = BauSettings.Load();
            var found = BauImporter.FindByPrefix(FilePrefix, includeImported: true, s);
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
