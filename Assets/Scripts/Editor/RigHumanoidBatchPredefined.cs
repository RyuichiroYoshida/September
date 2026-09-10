// Assets/Editor/RigHumanoidBatchPredefined.cs
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class RigHumanoidBatchPredefined
{
    // 対象フォルダ（必要ならここに追記）
    private static readonly string[] TARGET_FOLDERS = new[]
    {
        "Assets/Model/HARU",
        "Assets/Model/OKB",
        "Assets/Model/TANIHIRA",
        "Assets/Model/KOINUMA",
    };

    [MenuItem("Tools/Rig/Convert Predefined Folders to Humanoid")]
    private static void ConvertPredefinedFolders()
    {
        var validFolders = Array.FindAll(TARGET_FOLDERS, AssetDatabase.IsValidFolder);
        if (validFolders.Length == 0)
        {
            Debug.LogWarning("[RigHumanoidBatch] None of the predefined folders exist.");
            return;
        }

        ConvertModels(AssetDatabase.FindAssets("t:Model", validFolders), "predefined folders");
    }

    [MenuItem("Tools/Rig/Convert Selected Models to Humanoid")]
    private static void ConvertSelectedModels()
    {
        var selectedGuids = Selection.objects
            .Select(AssetDatabase.GetAssetPath)
            .Where(path => !string.IsNullOrEmpty(path))
            .SelectMany(FindModelGuids)
            .Distinct()
            .ToArray();

        ConvertModels(selectedGuids, "selection");
    }

    [MenuItem("Tools/Rig/Convert Selected Models to Humanoid", true)]
    private static bool CanConvertSelectedModels()
    {
        return Selection.objects.Any(asset => !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(asset)));
    }

    private static IEnumerable<string> FindModelGuids(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return AssetDatabase.FindAssets("t:Model", new[] { path });
        }

        return AssetImporter.GetAtPath(path) is ModelImporter
            ? new[] { AssetDatabase.AssetPathToGUID(path) }
            : Array.Empty<string>();
    }

    private static void ConvertModels(IEnumerable<string> guids, string source)
    {
        var modelGuids = guids.ToArray();
        if (modelGuids.Length == 0)
        {
            Debug.LogWarning($"[RigHumanoidBatch] No model assets were found in {source}.");
            return;
        }

        int changed = 0, skipped = 0, failed = 0, totalFbx = 0;

        AssetDatabase.StartAssetEditing();
        try
        {
            for (int i = 0; i < modelGuids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(modelGuids[i]);
                if (!path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) { skipped++; continue; }
                totalFbx++;

                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) { skipped++; continue; }

                bool needReimport = false;

                if (importer.animationType != ModelImporterAnimationType.Human)
                {
                    importer.animationType = ModelImporterAnimationType.Human;
                    needReimport = true;
                }

#if UNITY_2018_1_OR_NEWER
                if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
                {
                    importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    needReimport = true;
                }
#endif

                try
                {
                    if (needReimport)
                    {
                        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                        changed++;
                    }
                    else
                    {
                        skipped++;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[RigHumanoidBatch] Failed: {path}\n{e}");
                    failed++;
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
        }

        if (source == "predefined folders")
        {
            foreach (var folder in TARGET_FOLDERS)
            {
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    Debug.LogWarning($"[RigHumanoidBatch] Folder was not found: {folder}");
                }
            }
        }

        Debug.Log($"[RigHumanoidBatch] Complete ({source}). FBX: {totalFbx}, Changed: {changed}, Skipped: {skipped}, Failed: {failed}");
    }
}
#endif
