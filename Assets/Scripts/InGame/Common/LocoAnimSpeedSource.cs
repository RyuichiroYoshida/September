using UnityEngine;

namespace InGame.Common
{
    /// <summary>
    /// 移動クリップの「1 倍速で進む速さ (m/s)」を決める。
    /// クリップにルート移動が焼かれていれば AnimationClip.averageSpeed から算出し、
    /// 無ければ Inspector の手入力値へ明示的に (警告付きで) 切り替える。
    /// 実測速度 ÷ この値 を再生レートにすると歩幅と移動量が一致し、足滑りが消える。
    /// </summary>
    public static class LocoAnimSpeedSource
    {
        /// <summary>これ未満の averageSpeed は「移動量が焼かれていない (in-place)」とみなす。</summary>
        private const float MinBakedSpeed = 0.05f;

        public static float Resolve(AnimationClip clip, float fallbackSpeed, string label, Object context)
        {
            if (clip == null)
            {
                return fallbackSpeed;
            }

            Vector3 planar = clip.averageSpeed;
            planar.y = 0f;
            float bakedSpeed = planar.magnitude;
            if (bakedSpeed < MinBakedSpeed)
            {
                Debug.LogWarning(
                    $"[LocoAnimSpeedSource] {clip.name} にルート移動量が焼かれていない (averageSpeed={bakedSpeed:F3})。" +
                    $"{label} の手入力値 {fallbackSpeed:F2} m/s を使用します。", context);
                return fallbackSpeed;
            }

            return bakedSpeed;
        }
    }
}
