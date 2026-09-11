#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace September.Editor.HumanoidRig
{
    /// <summary>
    /// アニメーションを持つ FBX の全クリップに AvatarMask を一括で割り当てる / 解除する。
    /// ModelImporter のクリップ設定 (Animation タブの Mask) を書き換えて再インポートする。
    /// </summary>
    internal static class AnimationAvatarMaskApplier
    {
        /// <summary>全クリップに AvatarMask を割り当てる。戻り値は適用したクリップ数。</summary>
        public static int Apply(string assetPath, AvatarMask mask)
        {
            if (mask == null) throw new ArgumentNullException(nameof(mask), "AvatarMask が未指定です");

            var importer = ModelReimporter.RequireImporter(assetPath);
            var clips = RequireClips(importer, assetPath);
            foreach (var clip in clips)
            {
                clip.maskType = ClipAnimationMaskType.CopyFromOther;
                clip.maskSource = mask;
            }
            Write(importer, clips);
            return clips.Length;
        }

        /// <summary>全クリップのマスク設定を解除する。戻り値は対象クリップ数。</summary>
        public static int Clear(string assetPath)
        {
            var importer = ModelReimporter.RequireImporter(assetPath);
            var clips = RequireClips(importer, assetPath);
            foreach (var clip in clips)
            {
                clip.maskType = ClipAnimationMaskType.None;
                clip.maskSource = null;
            }
            Write(importer, clips);
            return clips.Length;
        }

        /// <summary>インポート設定上のクリップ数。0 ならアニメーションを持たない FBX。</summary>
        public static int CountClips(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            return importer == null ? 0 : ResolveClips(importer).Length;
        }

        /// <summary>一覧表示用に、クリップ数と現在のマスク設定を要約する。</summary>
        public static string DescribeMask(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null) return "ModelImporter なし";

            var clips = ResolveClips(importer);
            if (clips.Length == 0) return "アニメーションクリップなし";

            var masks = clips.Select(DescribeClipMask).Distinct().OrderBy(s => s, StringComparer.Ordinal);
            return $"クリップ {clips.Length} 件 / マスク: {string.Join(", ", masks)}";
        }

        private static string DescribeClipMask(ModelImporterClipAnimation clip)
        {
            if (clip.maskType != ClipAnimationMaskType.CopyFromOther) return clip.maskType.ToString();
            return clip.maskSource != null ? clip.maskSource.name : "CopyFromOther(未設定)";
        }

        /// <summary>
        /// 明示設定済みのクリップが無い FBX は defaultClipAnimations (取り込み時の既定クリップ) を使う。
        /// これを clipAnimations に書き戻すことで初めてマスク設定が保存できる。
        /// </summary>
        private static ModelImporterClipAnimation[] ResolveClips(ModelImporter importer)
        {
            var clips = importer.clipAnimations;
            return clips != null && clips.Length > 0 ? clips : importer.defaultClipAnimations;
        }

        private static ModelImporterClipAnimation[] RequireClips(ModelImporter importer, string assetPath)
        {
            var clips = ResolveClips(importer);
            if (clips.Length == 0)
            {
                throw new InvalidOperationException($"アニメーションクリップを持たないモデルです: {assetPath}");
            }
            return clips;
        }

        private static void Write(ModelImporter importer, ModelImporterClipAnimation[] clips)
        {
            importer.clipAnimations = clips;
            ModelReimporter.Apply(importer);
        }
    }
}
#endif
