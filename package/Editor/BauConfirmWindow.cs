using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Kyogetsukan.MochiApude
{
    /// <summary>
    /// 新しいファイルが見つかった時の確認ダイアログ。
    /// 下部に「次回からこのダイアログを表示しない」。外すと以後は黙って取り込む（設定から戻せる）。
    /// </summary>
    public class BauConfirmWindow : EditorWindow
    {
        List<BauImporter.Candidate> _items;
        BauSettings _settings;
        bool _dontShowAgain;
        Vector2 _scroll;

        public static void Open(List<BauImporter.Candidate> items, BauSettings settings)
        {
            var w = GetWindow<BauConfirmWindow>(true, "もちアプデ — 新しいファイル", true);
            w._items = items;
            w._settings = settings;
            w.minSize = new Vector2(520, 260);
            w.position = new Rect(
                (Screen.currentResolution.width - 560) / 2f, (Screen.currentResolution.height - 360) / 2f, 560, 360);
            w.ShowUtility();
        }

        void OnGUI()
        {
            if (_items == null) { Close(); return; }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("保存フォルダに、このプロジェクトへまだ入れていないファイルがあります。", EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("取り込むものにチェックを付けてください。外したものは、このプロジェクトでは今後聞きません。", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(4);

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUI.skin.box);
            foreach (var c in _items)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    c.selected = EditorGUILayout.ToggleLeft(c.fileName, c.selected, GUILayout.ExpandWidth(true));
                    GUILayout.Label($"{c.size / 1024f / 1024f:0.0} MB  {c.modified:MM/dd HH:mm}", EditorStyles.miniLabel, GUILayout.Width(130));
                }
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.HelpBox(
                "更新で互換性が変わるツール（変換プロファイル等）は、取り込む前に配布ページの更新履歴を確認してください。",
                MessageType.Info);

            EditorGUILayout.Space(4);
            _dontShowAgain = EditorGUILayout.ToggleLeft("次回からこのダイアログを表示しない（見つけたら自動で取り込む）", _dontShowAgain);

            EditorGUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("すべて選択", GUILayout.Width(90))) _items.ForEach(i => i.selected = true);
                if (GUILayout.Button("すべて外す", GUILayout.Width(90))) _items.ForEach(i => i.selected = false);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("後で", GUILayout.Width(90), GUILayout.Height(26)))
                {
                    // 何も記録しない。次回また聞く。
                    Close();
                }
                if (GUILayout.Button("取り込む", GUILayout.Width(110), GUILayout.Height(26)))
                {
                    if (_dontShowAgain) { _settings.confirmBeforeImport = false; _settings.Save(); }
                    var items = _items; var s = _settings;
                    Close();
                    EditorApplication.delayCall += () => BauImporter.Import(items, s);
                }
            }
            EditorGUILayout.Space(6);
        }
    }
}
