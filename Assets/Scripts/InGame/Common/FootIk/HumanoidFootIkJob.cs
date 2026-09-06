using UnityEngine;
using UnityEngine.Animations;

namespace InGame.Common.FootIk
{
    /// <summary>
    /// Humanoid の足 IK ゴールを地面高さに合わせるアニメーションジョブ。
    /// AnimationClipPlayer のレイヤーミキサー出力の直後で評価され、
    /// 各足の IK ゴールを「地面より下に沈まない」高さへ持ち上げてから SolveIK する。
    /// 接地情報の取得 (Raycast) はメインスレッド側 (HumanoidFootGroundIk) が行い、この構造体に渡す。
    /// </summary>
    public struct HumanoidFootIkJob : IAnimationJob
    {
        /// <summary>全体の適用率 (0 で素通し)。</summary>
        public float Weight;

        public FootTarget Left;
        public FootTarget Right;

        public struct FootTarget
        {
            /// <summary>接地判定が取れたか (1 = 有効、0 = 無効)。bool はジョブ構造体で使えないため float。</summary>
            public float Valid;
            /// <summary>足ボーンを置くべき最低高さ (ワールド Y)。</summary>
            public float GroundY;
            /// <summary>地面法線 (足首の傾き合わせ用)。</summary>
            public Vector3 Normal;
            /// <summary>法線に足を沿わせるか (1 = 有効)。</summary>
            public float AlignToNormal;
        }

        public void ProcessRootMotion(AnimationStream stream)
        {
        }

        public void ProcessAnimation(AnimationStream stream)
        {
            if (Weight <= 0f || !stream.isHumanStream) return;

            var human = stream.AsHuman();
            bool leftApplied = Apply(ref human, AvatarIKGoal.LeftFoot, in Left);
            bool rightApplied = Apply(ref human, AvatarIKGoal.RightFoot, in Right);
            if (leftApplied || rightApplied)
            {
                human.SolveIK();
            }
        }

        private bool Apply(ref AnimationHumanStream human, AvatarIKGoal goal, in FootTarget target)
        {
            if (target.Valid <= 0f)
            {
                human.SetGoalWeightPosition(goal, 0f);
                human.SetGoalWeightRotation(goal, 0f);
                return false;
            }

            // アニメーション本来の足位置を尊重し、地面より下に沈む分だけ持ち上げる (蹴り上げ等の浮きは維持)。
            Vector3 position = human.GetGoalPosition(goal);
            if (position.y >= target.GroundY)
            {
                human.SetGoalWeightPosition(goal, 0f);
                human.SetGoalWeightRotation(goal, 0f);
                return false;
            }

            position.y = target.GroundY;
            human.SetGoalPosition(goal, position);
            human.SetGoalWeightPosition(goal, Weight);

            if (target.AlignToNormal > 0f)
            {
                Quaternion rotation = Quaternion.FromToRotation(Vector3.up, target.Normal) * human.GetGoalRotation(goal);
                human.SetGoalRotation(goal, rotation);
                human.SetGoalWeightRotation(goal, Weight);
            }
            else
            {
                human.SetGoalWeightRotation(goal, 0f);
            }
            return true;
        }
    }
}
