#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace September.Editor.HumanoidRig
{
    /// <summary>
    /// 1 つのモデルアセットを検査し、Humanoid リグとしての問題を HumanoidRigReport にまとめる。
    /// 検査のみで、アセットは変更しない。
    /// </summary>
    internal static class HumanoidRigDiagnoser
    {
        public static HumanoidRigReport Diagnose(string assetPath)
        {
            var importer = ModelReimporter.RequireImporter(assetPath);
            var report = new HumanoidRigReport(assetPath, importer.animationType, importer.avatarSetup);

            CollectImportLog(assetPath, report);

            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                report.Add(HumanoidRigIssue.NotHumanoid, $"Animation Type が {importer.animationType}");
                return report;
            }

            bool avatarOk = CheckAvatar(assetPath, importer, report);
            if (avatarOk && importer.avatarSetup == ModelImporterAvatarSetup.CreateFromThisModel)
            {
                CheckPose(assetPath, importer, report);
            }
            return report;
        }

        private static bool CheckAvatar(string assetPath, ModelImporter importer, HumanoidRigReport report)
        {
            bool copied = importer.avatarSetup == ModelImporterAvatarSetup.CopyFromOther;
            var avatar = ModelAvatarResolver.Resolve(assetPath, importer);

            // Copy From Other Avatar のモデルは Avatar サブアセットを持たない。
            // コピー元 Avatar を実効 Avatar として判定する。
            if (avatar == null)
            {
                if (copied)
                {
                    report.Add(HumanoidRigIssue.AvatarSourceMissing, "Copy From Other Avatar だがコピー元 Avatar が未設定");
                    return false;
                }
                report.Add(HumanoidRigIssue.AvatarInvalid, "Avatar が生成されていないか無効");
                AddMissingRequired(importer, report);
                return false;
            }

            if (!avatar.isValid)
            {
                report.Add(HumanoidRigIssue.AvatarInvalid,
                    copied ? $"コピー元 Avatar が無効: {avatar.name}" : "Avatar が生成されていないか無効");
                if (!copied) AddMissingRequired(importer, report);
                return false;
            }

            if (!avatar.isHuman)
            {
                report.Add(HumanoidRigIssue.AvatarNotHuman,
                    copied ? $"コピー元 Avatar が Humanoid ではない: {avatar.name}" : "Avatar が Humanoid として成立していない");
                if (!copied) AddMissingRequired(importer, report);
                return false;
            }
            return true;
        }

        private static void AddMissingRequired(ModelImporter importer, HumanoidRigReport report)
        {
            var human = importer.humanDescription.human;
            if (human == null || human.Length == 0) return; // 自動割当前で情報が無い場合は列挙できない

            var missing = HumanoidRequiredBones.FindMissing(human.Select(h => h.humanName));
            if (missing.Count > 0)
            {
                report.Add(HumanoidRigIssue.MissingRequiredBones, "必須ボーン未割当: " + string.Join(", ", missing));
            }
        }

        private static void CheckPose(string assetPath, ModelImporter importer, HumanoidRigReport report)
        {
            var human = importer.humanDescription.human;
            if (human == null || human.Length == 0) return; // 割当が importer に保存されていなければ判定不能

            try
            {
                var prefab = ModelReimporter.RequireModelPrefab(assetPath);
                bool valid = AvatarSetupToolBridge.IsPoseValidOnInstance(prefab, new SerializedObject(importer));
                if (!valid) report.Add(HumanoidRigIssue.PoseInvalid, "T-Pose が崩れている (Enforce T-Pose が必要)");
            }
            catch (InvalidOperationException e)
            {
                // 内部 API 束縛失敗は隠さず「判定不可」として表示する。
                report.Add(HumanoidRigIssue.PoseUnknown, e.Message);
            }
        }

        private static void CollectImportLog(string assetPath, HumanoidRigReport report)
        {
            var log = AssetDatabase.LoadAssetAtPath<ImportLog>(assetPath);
            if (log == null || log.logEntries == null) return;

            foreach (var entry in log.logEntries)
            {
                if (entry.flags == ImportLogFlags.Error)
                {
                    report.Add(HumanoidRigIssue.ImportError, entry.message);
                }
                else if (entry.flags == ImportLogFlags.Warning)
                {
                    report.Add(HumanoidRigIssue.ImportWarning, entry.message);
                }
            }
        }
    }
}
#endif
