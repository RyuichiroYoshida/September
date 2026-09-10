#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace September.Editor.HumanoidRig
{
    /// <summary>
    /// 基準モデル (スケルトンの正) に対して、アニメーション FBX が整合しているかを検査する。
    /// ボーン階層の差分と、アニメーションカーブが基準階層で解決できるかを見る。検査のみでアセットは変更しない。
    /// </summary>
    internal static class ModelConsistencyChecker
    {
        public static ModelConsistencyReport Check(GameObject referenceRoot, string referencePath, string assetPath)
        {
            if (referenceRoot == null) throw new ArgumentNullException(nameof(referenceRoot), "基準モデルが未指定です");

            var report = new ModelConsistencyReport(assetPath);
            var targetRoot = ModelReimporter.RequireModelPrefab(assetPath);

            var referencePaths = ModelBoneHierarchy.CollectPaths(referenceRoot);
            CompareHierarchy(referencePaths, ModelBoneHierarchy.CollectPaths(targetRoot), report);
            CheckCurves(referencePaths, assetPath, report);
            CheckAnimationType(referencePath, assetPath, report);
            return report;
        }

        private static void CompareHierarchy(HashSet<string> referencePaths, HashSet<string> targetPaths, ModelConsistencyReport report)
        {
            var reference = ModelBoneHierarchy.GroupByName(referencePaths);
            var target = ModelBoneHierarchy.GroupByName(targetPaths);

            report.MissingBones.AddRange(reference.Keys.Where(name => !target.ContainsKey(name)).OrderBy(n => n, StringComparer.Ordinal));
            report.ExtraBones.AddRange(target.Keys.Where(name => !reference.ContainsKey(name)).OrderBy(n => n, StringComparer.Ordinal));

            foreach (var name in reference.Keys.Where(target.ContainsKey).OrderBy(n => n, StringComparer.Ordinal))
            {
                // 同名ボーンが複数ある階層はパス比較の対応付けが決まらないため、単一のものだけ突き合わせる。
                var referenceOwn = reference[name];
                var targetOwn = target[name];
                if (referenceOwn.Count != 1 || targetOwn.Count != 1) continue;
                if (string.Equals(referenceOwn[0], targetOwn[0], StringComparison.Ordinal)) continue;

                report.MismatchedPaths.Add($"{name} (基準: {referenceOwn[0]} / 対象: {targetOwn[0]})");
            }
        }

        private static void CheckCurves(HashSet<string> referencePaths, string assetPath, ModelConsistencyReport report)
        {
            var clips = AnimationClipBindings.LoadClips(assetPath);
            report.ClipCount = clips.Count;
            if (clips.Count == 0)
            {
                report.Notes.Add("アニメーションクリップを含まない FBX");
                return;
            }

            var unresolved = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var clip in clips)
            {
                foreach (var path in AnimationClipBindings.CollectCurvePaths(clip))
                {
                    if (!referencePaths.Contains(path)) unresolved.Add(path);
                }
            }
            report.UnresolvedCurvePaths.AddRange(unresolved);
        }

        private static void CheckAnimationType(string referencePath, string assetPath, ModelConsistencyReport report)
        {
            var target = ModelReimporter.RequireImporter(assetPath);
            var reference = AssetImporter.GetAtPath(referencePath) as ModelImporter;
            if (reference == null || reference.animationType == target.animationType) return;

            report.Notes.Add($"Animation Type が基準と異なる (基準: {reference.animationType} / 対象: {target.animationType})");
        }
    }
}
#endif
