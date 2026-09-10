#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace September.Editor.HumanoidRig
{
    /// <summary>
    /// 命名規則ベースのボーン割当で使う別名テーブル。
    /// キーは「左右トークンと区切り文字を取り除き小文字化した core 名」。
    /// Mixamo / Blender (Rigify) / VRM / Unity 標準名 / 3ds Max Biped 系を網羅する。
    /// 脊椎チェーン (spine, spine1, spine2 ...) と指は HumanoidBoneNameMapper 側で規則的に処理する。
    /// </summary>
    internal static class HumanoidBoneNameRules
    {
        /// <summary>左右を持たない中心ボーンの別名。</summary>
        public static readonly IReadOnlyDictionary<string, HumanBodyBones> CenterAliases =
            new Dictionary<string, HumanBodyBones>
            {
                { "hips", HumanBodyBones.Hips },
                { "hip", HumanBodyBones.Hips },
                { "pelvis", HumanBodyBones.Hips },
                { "chest", HumanBodyBones.Chest },
                { "upperchest", HumanBodyBones.UpperChest },
                { "chest2", HumanBodyBones.UpperChest },
                { "neck", HumanBodyBones.Neck },
                { "neck1", HumanBodyBones.Neck },
                { "head", HumanBodyBones.Head },
                { "jaw", HumanBodyBones.Jaw },
            };

        /// <summary>左右を持つボーンの別名。左右は HumanoidBoneNameMapper が名前から判定する。</summary>
        public static readonly IReadOnlyDictionary<string, SidedBone> SidedAliases =
            new Dictionary<string, SidedBone>
            {
                { "shoulder", SidedBone.Shoulder },
                { "clavicle", SidedBone.Shoulder },
                { "collar", SidedBone.Shoulder },
                { "upperarm", SidedBone.UpperArm },
                { "uparm", SidedBone.UpperArm },
                { "arm", SidedBone.UpperArm },
                { "lowerarm", SidedBone.LowerArm },
                { "lowarm", SidedBone.LowerArm },
                { "forearm", SidedBone.LowerArm },
                { "elbow", SidedBone.LowerArm },
                { "hand", SidedBone.Hand },
                { "wrist", SidedBone.Hand },
                { "upperleg", SidedBone.UpperLeg },
                { "upleg", SidedBone.UpperLeg },
                { "thigh", SidedBone.UpperLeg },
                { "hipjoint", SidedBone.UpperLeg },
                { "lowerleg", SidedBone.LowerLeg },
                { "lowleg", SidedBone.LowerLeg },
                { "leg", SidedBone.LowerLeg }, // Mixamo は UpLeg / Leg の対
                { "shin", SidedBone.LowerLeg },
                { "calf", SidedBone.LowerLeg },
                { "knee", SidedBone.LowerLeg },
                { "foot", SidedBone.Foot },
                { "ankle", SidedBone.Foot },
                { "toes", SidedBone.Toes },
                { "toe", SidedBone.Toes },
                { "toebase", SidedBone.Toes },
                { "ball", SidedBone.Toes },
                { "eye", SidedBone.Eye },
            };

        /// <summary>指名 → (左手の近位ボーン, 右手の近位ボーン)。近位から順に 3 節が enum で連続している。</summary>
        public static readonly IReadOnlyDictionary<string, (HumanBodyBones left, HumanBodyBones right)> FingerProximal =
            new Dictionary<string, (HumanBodyBones, HumanBodyBones)>
            {
                { "thumb", (HumanBodyBones.LeftThumbProximal, HumanBodyBones.RightThumbProximal) },
                { "index", (HumanBodyBones.LeftIndexProximal, HumanBodyBones.RightIndexProximal) },
                { "middle", (HumanBodyBones.LeftMiddleProximal, HumanBodyBones.RightMiddleProximal) },
                { "ring", (HumanBodyBones.LeftRingProximal, HumanBodyBones.RightRingProximal) },
                { "pinky", (HumanBodyBones.LeftLittleProximal, HumanBodyBones.RightLittleProximal) },
                { "little", (HumanBodyBones.LeftLittleProximal, HumanBodyBones.RightLittleProximal) },
            };

        /// <summary>名前から取り除く既知の接頭辞 (小文字比較)。</summary>
        public static readonly IReadOnlyList<string> StripPrefixes = new[]
        {
            "mixamorig:", "mixamorig", "j_bip_c_", "j_bip_", "bip001", "bip01", "bip", "def-", "b_", "bone_",
        };

        public static HumanBodyBones Resolve(SidedBone bone, BoneSide side)
        {
            bool isLeft = side == BoneSide.Left;
            switch (bone)
            {
                case SidedBone.Shoulder: return isLeft ? HumanBodyBones.LeftShoulder : HumanBodyBones.RightShoulder;
                case SidedBone.UpperArm: return isLeft ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm;
                case SidedBone.LowerArm: return isLeft ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm;
                case SidedBone.Hand: return isLeft ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;
                case SidedBone.UpperLeg: return isLeft ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg;
                case SidedBone.LowerLeg: return isLeft ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg;
                case SidedBone.Foot: return isLeft ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot;
                case SidedBone.Toes: return isLeft ? HumanBodyBones.LeftToes : HumanBodyBones.RightToes;
                case SidedBone.Eye: return isLeft ? HumanBodyBones.LeftEye : HumanBodyBones.RightEye;
                default: return HumanBodyBones.LastBone;
            }
        }
    }

    internal enum SidedBone
    {
        Shoulder, UpperArm, LowerArm, Hand, UpperLeg, LowerLeg, Foot, Toes, Eye,
    }

    internal enum BoneSide
    {
        None, Left, Right,
    }
}
#endif
