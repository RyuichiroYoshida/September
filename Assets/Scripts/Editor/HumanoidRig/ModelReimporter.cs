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

        /// <summary>
        /// importer の変更を保存して再インポートする。
        /// SaveAndReimport は StartAssetEditing/StopAssetEditing の一括編集中に呼ぶと
        /// インポートが遅延して取りこぼされるため、バッチ中は SaveAssets + ImportAsset を使う
        /// (RigHumanoidBatchPredefined と同じ形)。
        /// </summary>
        public static void Apply(ModelImporter importer)
        {
            EditorUtility.SetDirty(importer);
            if (IsBatching)
            {
                AssetDatabase.WriteImportSettingsIfDirty(importer.assetPath);
                AssetDatabase.ImportAsset(importer.assetPath, ImportAssetOptions.ForceUpdate);
                return;
            }
            importer.SaveAndReimport();
        }

        /// <summary>AssetDatabase.StartAssetEditing による一括編集中かどうか。</summary>
        public static bool IsBatching { get; private set; }

        /// <summary>一括編集の開始/終了を ModelReimporter に伝える (HumanoidRigBatchRunner から使う)。</summary>
        public static void BeginBatch()
        {
            AssetDatabase.StartAssetEditing();
            IsBatching = true;
        }

        public static void EndBatch()
        {
            IsBatching = false;
            AssetDatabase.StopAssetEditing();
        }
    }
}
#endif
