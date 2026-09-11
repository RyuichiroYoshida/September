#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace September.Editor.HumanoidRig
{
    /// <summary>FBX に含まれる AnimationClip と、そのカーブが参照している Transform パスを取り出す。</summary>
    internal static class AnimationClipBindings
    {
        private const string PreviewClipPrefix = "__preview__";

        public static IReadOnlyList<AnimationClip> LoadClips(string assetPath)
        {
            return AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath)
                .OfType<AnimationClip>()
                .Where(clip => !clip.name.StartsWith(PreviewClipPrefix, StringComparison.Ordinal))
                .ToList();
        }

        /// <summary>クリップのカーブが指す相対パス集合 (ルート自身を指す空文字は除く)。</summary>
        public static HashSet<string> CollectCurvePaths(AnimationClip clip)
        {
            var paths = new HashSet<string>(StringComparer.Ordinal);
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (!string.IsNullOrEmpty(binding.path)) paths.Add(binding.path);
            }
            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                if (!string.IsNullOrEmpty(binding.path)) paths.Add(binding.path);
            }
            return paths;
        }
    }
}
#endif
