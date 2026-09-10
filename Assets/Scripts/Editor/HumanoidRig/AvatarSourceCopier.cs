#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace September.Editor.HumanoidRig
{
    /// <summary>基準モデルの Avatar を他の FBX に「Copy From Other Avatar」で流用し、リグ設定を統一する。</summary>
    internal static class AvatarSourceCopier
    {
        public static void Apply(string assetPath, Avatar source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source), "コピー元 Avatar が未指定です");
            if (!source.isValid || !source.isHuman)
            {
                throw new InvalidOperationException($"コピー元 Avatar が有効な Humanoid ではありません: {source.name}");
            }

            string sourcePath = AssetDatabase.GetAssetPath(source);
            if (string.Equals(sourcePath, assetPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"コピー元と同じモデルには適用できません: {assetPath}");
            }

            var importer = ModelReimporter.RequireImporter(assetPath);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            importer.sourceAvatar = source;
            ModelReimporter.Apply(importer);
        }
    }
}
#endif
