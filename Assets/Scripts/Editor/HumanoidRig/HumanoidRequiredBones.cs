#if UNITY_EDITOR
using System;
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

        private static readonly IReadOnlyDictionary<HumanBodyBones, string> HumanNames = BuildHumanNames();

        /// <summary>
        /// HumanBodyBones → HumanDescription.human で使う人間ボーン名。
        /// HumanTrait.BoneName の並びは HumanBodyBones の enum 値順とは一致しない
        /// (UpperChest が後方互換のため enum 末尾に追加されている等) ため、
        /// 添字ではなく名前一致で引く。BoneName の各要素は enum 名と同じ綴り。
        /// </summary>
        public static string ToHumanName(HumanBodyBones bone)
        {
            if (HumanNames.TryGetValue(bone, out var name)) return name;
            throw new ArgumentOutOfRangeException(
                nameof(bone), bone, "HumanTrait.BoneName に対応する人間ボーン名がありません");
        }

        private static Dictionary<HumanBodyBones, string> BuildHumanNames()
        {
            var byName = new HashSet<string>(HumanTrait.BoneName, StringComparer.Ordinal);

            var map = new Dictionary<HumanBodyBones, string>();
            foreach (HumanBodyBones bone in Enum.GetValues(typeof(HumanBodyBones)))
            {
                if (bone == HumanBodyBones.LastBone) continue;
                string name = bone.ToString();
                if (byName.Contains(name)) map[bone] = name;
            }
            return map;
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
