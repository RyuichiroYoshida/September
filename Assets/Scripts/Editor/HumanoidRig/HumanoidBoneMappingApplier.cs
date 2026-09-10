#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace September.Editor.HumanoidRig
{
    /// <summary>
    /// ボーン割当を ModelImporter.humanDescription に書き込んで再インポートする。
    /// 命名規則による割当 (HumanoidBoneNameMapper) と、Unity 標準の自動割当への差し戻しを提供する。
    /// </summary>
    internal static class HumanoidBoneMappingApplier
    {
        /// <summary>命名規則で推定した割当を適用する。必須ボーンが欠ける場合は適用せず例外で詳細を返す。</summary>
        public static HumanoidBoneNameMapping ApplyByName(string assetPath)
        {
            var prefab = ModelReimporter.RequireModelPrefab(assetPath);
            var mapping = HumanoidBoneNameMapper.Map(prefab.transform);

            if (mapping.MissingRequired.Count > 0)
            {
                throw new InvalidOperationException(
                    $"命名規則から必須ボーンを特定できないため適用しません: {assetPath}\n{mapping.Summarize()}");
            }

            var importer = ModelReimporter.RequireImporter(assetPath);
            var description = importer.humanDescription;
            description.human = BuildHumanBones(mapping.Assigned);
            description.skeleton = BuildSkeletonBones(prefab.transform);
            importer.humanDescription = description;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            ModelReimporter.Apply(importer);
            return mapping;
        }

        /// <summary>手動 / スクリプト割当を破棄し、Unity 標準の自動ボーン割当で再インポートする。</summary>
        public static void ResetToAutoMapping(string assetPath)
        {
            var importer = ModelReimporter.RequireImporter(assetPath);
            var description = importer.humanDescription;
            // human と skeleton を空にすると、インポート時に Unity 側 (AvatarAutoMapper) が割当をやり直す。
            description.human = Array.Empty<HumanBone>();
            description.skeleton = Array.Empty<SkeletonBone>();
            importer.humanDescription = description;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            ModelReimporter.Apply(importer);
        }

        private static HumanBone[] BuildHumanBones(IReadOnlyDictionary<HumanBodyBones, Transform> assigned)
        {
            return assigned
                .OrderBy(kv => (int)kv.Key)
                .Select(kv => new HumanBone
                {
                    humanName = HumanoidRequiredBones.ToHumanName(kv.Key),
                    boneName = kv.Value.name,
                    limit = new HumanLimit { useDefaultValues = true },
                })
                .ToArray();
        }

        /// <summary>モデルの現在の階層姿勢をスケルトン定義として書き出す (先頭はルート)。</summary>
        private static SkeletonBone[] BuildSkeletonBones(Transform root)
        {
            return root.GetComponentsInChildren<Transform>(true)
                .Select(t => new SkeletonBone
                {
                    name = t.name,
                    position = t.localPosition,
                    rotation = t.localRotation,
                    scale = t.localScale,
                })
                .ToArray();
        }
    }
}
#endif
