#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace September.Editor.HumanoidRig
{
    /// <summary>ModelImporter の取得と、設定変更の確定 (再インポート) の共通入口。</summary>
    internal static class ModelReimporter
    {
        public static ModelImporter RequireImporter(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null)
            {
                throw new InvalidOperationException($"ModelImporter を取得できません (FBX ではない可能性): {assetPath}");
            }
            return importer;
        }

        public static GameObject RequireModelPrefab(string assetPath)
        {
            var prefab = AssetDatabase.LoadMainAssetAtPath(assetPath) as GameObject;
            if (prefab == null)
            {
                throw new InvalidOperationException($"モデルのルート GameObject を読み込めません: {assetPath}");
            }
            return prefab;
        }

        public static void Apply(ModelImporter importer)
        {
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }
    }
}
#endif
