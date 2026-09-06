using UnityEngine;

namespace September.Common.Performance
{
    /// <summary>
    /// 起動時にフレームレート上限を適用する。
    /// <para>
    /// QualitySettings の Mobile / PC いずれのプロファイルも vSyncCount が 0 のため、
    /// 上限を設けないとフレームレートが青天井になり、フレームペーシングの乱れと
    /// GPU の無駄な発熱を招く。vSync を有効化した環境では Unity 側が
    /// targetFrameRate を無視するので、ここでの設定は無害。
    /// </para>
    /// </summary>
    public static class FrameRateSettings
    {
        /// <summary> ゲームプレイ中のフレームレート上限 (fps)。 </summary>
        private const int TargetFrameRate = 60;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            Application.targetFrameRate = TargetFrameRate;
        }
    }
}
