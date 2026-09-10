#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace September.Editor.HumanoidRig
{
    internal enum HumanoidRigIssue
    {
        NotHumanoid,
        AvatarInvalid,
        AvatarNotHuman,
        AvatarSourceMissing,
        MissingRequiredBones,
        PoseInvalid,
        PoseUnknown,
        ImportError,
        ImportWarning,
    }

    /// <summary>1 モデル分の診断結果 (データのみ)。</summary>
    internal sealed class HumanoidRigReport
    {
        public string AssetPath { get; }
        public ModelImporterAnimationType AnimationType { get; }
        public ModelImporterAvatarSetup AvatarSetup { get; }
        public List<(HumanoidRigIssue issue, string detail)> Issues { get; } = new List<(HumanoidRigIssue, string)>();

        public HumanoidRigReport(string assetPath, ModelImporterAnimationType animationType, ModelImporterAvatarSetup avatarSetup)
        {
            AssetPath = assetPath;
            AnimationType = animationType;
            AvatarSetup = avatarSetup;
        }

        public bool HasProblem => Issues.Count > 0;

        /// <summary>警告以外の、修正が必要な問題を含むか。</summary>
        public bool HasError => Issues.Any(i => i.issue != HumanoidRigIssue.ImportWarning && i.issue != HumanoidRigIssue.PoseUnknown);

        public bool Has(HumanoidRigIssue issue) => Issues.Any(i => i.issue == issue);

        public void Add(HumanoidRigIssue issue, string detail) => Issues.Add((issue, detail));

        public string StatusLabel
        {
            get
            {
                if (!HasProblem) return "OK";
                return HasError ? "要修正" : "警告";
            }
        }

        public string IssueSummary => string.Join("\n", Issues.Select(i => $"[{i.issue}] {i.detail}"));
    }
}
#endif
