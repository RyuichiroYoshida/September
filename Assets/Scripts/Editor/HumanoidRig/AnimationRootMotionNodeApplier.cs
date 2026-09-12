#if UNITY_EDITOR
using System;
using System.Linq;

namespace September.Editor.HumanoidRig
{
    internal static class AnimationRootMotionNodeApplier
    {
        // Unity の Animation インポーターが保存する特殊値。
        internal const string RootTransform = "<Root Transform>";
        public static string Apply(string assetPath, string node, bool clear = false)
        {
            var importer = ModelReimporter.RequireImporter(assetPath);
            if (!importer.importAnimation || AnimationAvatarMaskApplier.CountClips(assetPath) == 0)
                throw new InvalidOperationException("アニメーションが有効なモデルではありません。");

            string resolved = clear ? string.Empty : ResolvePath(importer.transformPaths, node);
            if (string.Equals(importer.motionNodeName, resolved, StringComparison.Ordinal))
                return "変更なし";

            importer.motionNodeName = resolved;
            ModelReimporter.Apply(importer);
            return string.IsNullOrEmpty(resolved) ? "None に変更" : $"Root Motion Node: {resolved}";
        }

        internal static string ResolvePath(string[] paths, string node)
        {
            if (string.IsNullOrWhiteSpace(node))
                throw new ArgumentException("ノード名または階層パスを指定してください。", nameof(node));

            if (node == RootTransform) return RootTransform;

            // 基準 FBX の候補から選んだパスを完全一致で適用する。
            var matches = (paths ?? Array.Empty<string>())
                .Where(p => !string.IsNullOrEmpty(p) &&
                    string.Equals(p, node, StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal).ToArray();
            if (matches.Length == 0)
                throw new InvalidOperationException($"ノードが見つかりません: {node}");
            if (matches.Length > 1)
                throw new InvalidOperationException($"同名ノードが複数あります。階層パスを指定してください: {string.Join(", ", matches)}");
            return matches[0];
        }
    }
}
#endif
