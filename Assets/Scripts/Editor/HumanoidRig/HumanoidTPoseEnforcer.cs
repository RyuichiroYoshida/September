#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace September.Editor.HumanoidRig
{
    internal enum TPoseResult
    {
        AlreadyValid,
        Fixed,
    }

    /// <summary>
    /// Avatar Configure 画面の "Pose → Enforce T-Pose" と同じ処理をアセットに対して直接行い、再インポートする。
    /// </summary>
    internal static class HumanoidTPoseEnforcer
    {
        public static TPoseResult Enforce(string assetPath)
        {
            var importer = ModelReimporter.RequireImporter(assetPath);
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                throw new InvalidOperationException($"Humanoid ではないため T-Pose 補正できません。先に Humanoid 化してください: {assetPath}");
            }
            if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
            {
                throw new InvalidOperationException($"Avatar を他モデルからコピーしているため T-Pose 補正の対象外です (コピー元を補正してください): {assetPath}");
            }

            var importerObject = new SerializedObject(importer);
            if (importerObject.FindProperty(AvatarSetupToolBridge.SkeletonArrayProperty).arraySize == 0)
            {
                throw new InvalidOperationException($"スケルトン定義が未生成です。Humanoid として一度インポートしてから実行してください: {assetPath}");
            }

            var prefab = ModelReimporter.RequireModelPrefab(assetPath);
            var instance = UnityEngine.Object.Instantiate(prefab);
            instance.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var modelBones = AvatarSetupToolBridge.GetModelBones(instance.transform);
                var humanBones = AvatarSetupToolBridge.GetHumanBones(
                    importerObject.FindProperty(AvatarSetupToolBridge.HumanArrayProperty), modelBones);

                // 保存済み姿勢をインスタンスに載せてから判定・補正し、補正後の姿勢を書き戻す。
                AvatarSetupToolBridge.TransferDescriptionToPose(importerObject, instance.transform);
                if (AvatarSetupToolBridge.IsPoseValid(humanBones))
                {
                    return TPoseResult.AlreadyValid;
                }

                AvatarSetupToolBridge.MakePoseValid(humanBones);
                AvatarSetupToolBridge.TransferPoseToDescription(
                    importerObject.FindProperty(AvatarSetupToolBridge.SkeletonArrayProperty), instance.transform);
                // 補正した姿勢は SerializedObject 経由でしか書けないため、
                // 再インポートを起こす前にここで .meta へ確定させる。
                importerObject.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.WriteImportSettingsIfDirty(assetPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }

            // SetDirty で importer 側の未反映値に上書きされないよう、確定済み設定をそのまま読み直させる。
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            return TPoseResult.Fixed;
        }
    }
}
#endif
