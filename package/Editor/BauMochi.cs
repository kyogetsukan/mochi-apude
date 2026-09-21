#if !KYOGETSUBAKO_PRESENT
using UnityEditor;
using UnityEngine;

namespace Kyogetsukan.MochiApude
{
    /// <summary>
    /// もちアプデ単体ウィンドウ。境月箱が入っている時は KYOGETSUBAKO_PRESENT が立ち、
    /// このメニュー／ウィンドウは丸ごとコンパイルされない（＝箱の中に集約される）。
    /// </summary>
    public class BauMochi : EditorWindow
    {
        const string MenuRoot = "Tools/境月館/もちアプデ/";
        readonly MochiApudePanel _panel = new MochiApudePanel();

        [MenuItem(MenuRoot + "ウィンドウを開く", false, 0)]
        public static void Open()
        {
            var w = GetWindow<BauMochi>("もちアプデ");
            w.minSize = new Vector2(460, 300);
        }

        void OnEnable() { _panel.Repaint = Repaint; }

        void OnGUI()
        {
            EditorGUILayout.Space(8);
            _panel.Draw();
        }
    }
}
#endif
