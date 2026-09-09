using System;
using UnityEngine;

namespace September.Common
{
    /// <summary>Animatorからのボーンパスと、衣装・しっぽの揺れ方を保存する。</summary>
    [CreateAssetMenu(menuName = "September/Animation/Accessory Spring Profile")]
    public sealed class AccessorySpringProfile : ScriptableObject
    {
        [Serializable]
        public sealed class RootTuning
        {
            [Tooltip("RootPathsの要素と完全一致させる。空欄なら全体設定を使う。")]
            public string RootPath;
            [Range(0f, 1f), Tooltip("1で真下へ垂れ、0に近いほど元の伸びる向きを残す。")]
            public float GravityInfluence = 1f;
        }

        [Tooltip("Animatorから見た追加ボーンの根元への相対パス。人体ボーンは指定しない。")]
        public string[] RootPaths = new string[0];
        [Tooltip("尻尾など、RootPathsごとの垂れ具合。")]
        public RootTuning[] RootTunings = new RootTuning[0];
        [Min(0f)] public float Stiffness = 30f;
        [Min(0f)] public float Damping = 6f;
        [Min(0.01f), Tooltip("揺れの速度上限。走行・攻撃アニメーションによる跳ねを抑える。")]
        public float MaxVelocity = 0.8f;
        [Min(0.01f), Tooltip("アニメーション側の目標位置へ追従する速度上限。急な姿勢変化を滑らかにする。")]
        public float MaxTargetSpeed = 0.8f;
        [Range(0f, 1f), Tooltip("本体の移動を揺れへ変換する割合。高いほど走行時に後ろへ残る。")]
        public float Inertia = 0.15f;
        [Min(0.1f), Tooltip("揺れの計算速度。最終的な垂れ姿勢は変えず、姿勢が落ち着くまでの時間だけを調整する。")]
        public float SimulationSpeed = 1f;
        [Min(0f), Tooltip("重力姿勢とアニメーション姿勢の切り替え速度。")]
        public float DefaultPoseSpeed = 12f;
        [Min(0f), Tooltip("この速度を基準に、停止時は重力姿勢へ、動作中はアニメーション姿勢へ連続的に切り替える。")]
        public float DefaultPoseMotionThreshold = 0.2f;
        [Tooltip("アクセサリーが垂れる向き。通常はワールド下方向。")]
        public Vector3 Gravity = new Vector3(0f, -9.81f, 0f);
        [Range(0f, 1f)] public float Weight = 1f;
        [Min(0.001f)] public float TipLength = 0.08f;
        [Min(0f)] public float CollisionRadius = 0.015f;
        [Min(0.1f)] public float TeleportDistance = 2f;

        public float GetGravityInfluence(string rootPath)
        {
            if (RootTunings == null) return 1f;
            foreach (var tuning in RootTunings)
                if (tuning != null && tuning.RootPath == rootPath) return tuning.GravityInfluence;
            return 1f;
        }
    }
}
