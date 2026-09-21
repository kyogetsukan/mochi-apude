using UnityEngine;
using Kyogetsukan.Kyogetsubako;

namespace Kyogetsukan.MochiApude.Kyogetsubako
{
    /// <summary>
    /// 境月箱アダプター。境月箱が入っている時だけコンパイルされ（KYOGETSUBAKO_PRESENT）、
    /// もちアプデを境月箱の一覧に載せる。中身は単体ウィンドウと同じ MochiApudePanel。
    /// </summary>
    public class MochiApudeModule : IKyogetsukanModule
    {
        public string Title => "もちアプデ";
        public string Subtitle => "もちふぃった～ 更新";
        public string Description => "購入済みのもちふぃった～に新しい版が出ていれば、Unityから取得して取り込みます。";
        public Color Accent { get { ColorUtility.TryParseHtmlString("#2fd6bd", out var c); return c; } }

        readonly MochiApudePanel _panel = new MochiApudePanel();

        public void Draw() { _panel.Draw(); }
    }
}
