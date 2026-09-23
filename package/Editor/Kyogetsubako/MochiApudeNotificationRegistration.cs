using UnityEditor;
using Kyogetsukan.Kyogetsubako;

namespace Kyogetsukan.MochiApude.Kyogetsubako
{
    /// <summary>
    /// もちアプデの起動時確認（BauStartupCheck）の結果を、境月箱ボタンの赤ポチに繋げる。
    /// 境月箱が入っている時だけコンパイルされる（KYOGETSUBAKO_PRESENT）。
    /// </summary>
    [InitializeOnLoad]
    static class MochiApudeNotificationRegistration
    {
        static MochiApudeNotificationRegistration()
        {
            KyogetsubakoNotifications.Register(() => !string.IsNullOrEmpty(BauStartupCheck.LastMessage));
        }
    }
}
