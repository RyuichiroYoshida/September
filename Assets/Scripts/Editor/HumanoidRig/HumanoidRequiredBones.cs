#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace September.Editor.HumanoidRig
{
    /// <summary>
    /// Humanoid Avatar が成立するために必須の 15 ボーンと、
    /// HumanBodyBones と Unity 内部の人間ボーン名 ("LeftUpperArm" 等) の相互変換を提供する。
    /// </summary>
    internal static class HumanoidRequiredBones
    {
        public static readonly IReadOnlyList<HumanBodyBones> Required = new[]
        {
            HumanBodyBones.Hips,
            HumanBodyBones.Spine,
            HumanBodyBones.Head,
            HumanBodyBones.LeftUpperArm,
            HumanBodyBones.LeftLowerArm,
            HumanBodyBones.LeftHand,
            HumanBodyBones.RightUpperArm,
            HumanBodyBones.RightLowerArm,
            HumanBodyBones.RightHand,
            HumanBodyBones.LeftUpperLeg,
            HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.LeftFoot,
            HumanBodyBones.RightUpperLeg,
            HumanBodyBones.RightLowerLeg,
            HumanBodyBones.RightFoot,
        };

        /// <summary>HumanBodyBones → HumanDescription.human で使う人間ボーン名。</summary>
        public static string ToHumanName(HumanBodyBones bone)
        {
            // HumanTrait.BoneName は HumanBodyBones の enum 順 (LastBone を除く) と一致する。
            return HumanTrait.BoneName[(int)bone];
        }

        /// <summary>割当済み人間ボーン名の集合から、欠けている必須ボーンを列挙する。</summary>
        public static IReadOnlyList<HumanBodyBones> FindMissing(IEnumerable<string> assignedHumanNames)
        {
            var assigned = new HashSet<string>(assignedHumanNames);
            return Required.Where(b => !assigned.Contains(ToHumanName(b))).ToList();
        }
    }
}
#endif
