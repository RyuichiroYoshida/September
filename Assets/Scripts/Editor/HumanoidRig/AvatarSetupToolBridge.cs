#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace September.Editor.HumanoidRig
{
    /// <summary>
    /// Unity 内部 API UnityEditor.AvatarSetupTool (Avatar Configure 画面の "Enforce T-Pose" 等の実体) への
    /// リフレクション束縛。公開 API に T-Pose 補正が無いためここだけで内部依存を閉じる。
    /// 束縛に失敗した場合は黙って劣化せず InvalidOperationException で即座に失敗させる。
    /// </summary>
    internal static class AvatarSetupToolBridge
    {
        /// <summary>ModelImporter の SerializedObject 上の人間ボーン配列パス (AvatarSetupTool.sHuman と同値)。</summary>
        public const string HumanArrayProperty = "m_HumanDescription.m_Human";

        /// <summary>ModelImporter の SerializedObject 上のスケルトン配列パス (AvatarSetupTool.sSkeleton と同値)。</summary>
        public const string SkeletonArrayProperty = "m_HumanDescription.m_Skeleton";

        private const string ToolTypeName = "UnityEditor.AvatarSetupTool";
        private const BindingFlags StaticPublic = BindingFlags.Static | BindingFlags.Public;

        private static Binding _binding;

        private sealed class Binding
        {
            public MethodInfo GetModelBones;
            public MethodInfo GetHumanBones;
            public MethodInfo IsPoseValid;
            public MethodInfo MakePoseValid;
            public MethodInfo TransferDescriptionToPose;
            public MethodInfo TransferPoseToDescription;
            public MethodInfo IsPoseValidOnInstance;
        }

        public static bool IsPoseValidOnInstance(GameObject modelPrefab, SerializedObject importerObject)
        {
            return (bool)Bind().IsPoseValidOnInstance.Invoke(null, new object[] { modelPrefab, importerObject });
        }

        /// <summary>インスタンス階層からボーン候補 (Transform → 有効フラグ) を得る。</summary>
        public static object GetModelBones(Transform instanceRoot)
        {
            return Bind().GetModelBones.Invoke(null, new object[] { instanceRoot, false, null });
        }

        /// <summary>人間ボーン配列プロパティと候補から BoneWrapper[] (内部型) を得る。</summary>
        public static object GetHumanBones(SerializedProperty humanBoneArray, object modelBones)
        {
            return Bind().GetHumanBones.Invoke(null, new[] { humanBoneArray, modelBones });
        }

        public static bool IsPoseValid(object humanBones)
        {
            return (bool)Bind().IsPoseValid.Invoke(null, new[] { humanBones });
        }

        public static void MakePoseValid(object humanBones)
        {
            Bind().MakePoseValid.Invoke(null, new[] { humanBones });
        }

        /// <summary>importer に保存されたスケルトン姿勢をインスタンスへ反映する。</summary>
        public static void TransferDescriptionToPose(SerializedObject importerObject, Transform instanceRoot)
        {
            Bind().TransferDescriptionToPose.Invoke(null, new object[] { importerObject, instanceRoot });
        }

        /// <summary>インスタンスの現在姿勢を importer のスケルトン配列へ書き戻す。</summary>
        public static void TransferPoseToDescription(SerializedProperty skeletonBoneArray, Transform instanceRoot)
        {
            Bind().TransferPoseToDescription.Invoke(null, new object[] { skeletonBoneArray, instanceRoot });
        }

        private static Binding Bind()
        {
            if (_binding != null) return _binding;

            var toolType = typeof(UnityEditor.Editor).Assembly.GetType(ToolTypeName);
            if (toolType == null)
            {
                throw Unavailable($"型 {ToolTypeName} が見つかりません");
            }

            var methods = toolType.GetMethods(StaticPublic);
            _binding = new Binding
            {
                GetModelBones = Require(methods, "GetModelBones", typeof(Transform), typeof(bool)),
                GetHumanBones = Require(methods, "GetHumanBones", typeof(SerializedProperty), typeof(Dictionary<Transform, bool>)),
                IsPoseValid = Require(methods, "IsPoseValid"),
                MakePoseValid = Require(methods, "MakePoseValid"),
                TransferDescriptionToPose = Require(methods, "TransferDescriptionToPose", typeof(SerializedObject), typeof(Transform)),
                TransferPoseToDescription = Require(methods, "TransferPoseToDescription", typeof(SerializedProperty), typeof(Transform)),
                IsPoseValidOnInstance = Require(methods, "IsPoseValidOnInstance", typeof(GameObject), typeof(SerializedObject)),
            };
            return _binding;
        }

        /// <summary>名前と先頭引数型で一意に特定する。見つからなければ Unity バージョン差異として明示的に失敗する。</summary>
        private static MethodInfo Require(MethodInfo[] methods, string name, params Type[] leadingParameterTypes)
        {
            var candidates = methods.Where(m => m.Name == name).Where(m =>
            {
                var ps = m.GetParameters();
                if (ps.Length < leadingParameterTypes.Length) return false;
                return !leadingParameterTypes.Where((t, i) => ps[i].ParameterType != t).Any();
            }).ToList();

            if (candidates.Count == 1) return candidates[0];
            throw Unavailable($"{ToolTypeName}.{name} を一意に特定できません (候補 {candidates.Count} 件)");
        }

        private static InvalidOperationException Unavailable(string detail)
        {
            return new InvalidOperationException(
                $"T-Pose 補正に使う Unity 内部 API に束縛できません。Unity {Application.unityVersion} で内部 API が変更された可能性があります: {detail}");
        }
    }
}
#endif
