#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace September.Editor.HumanoidRig
{
    /// <summary>
    /// モデルに実際に適用されている Avatar を取得する。
    /// Copy From Other Avatar のモデルは自前の Avatar サブアセットを持たないため、
    /// アセットパスから読むだけでは「Avatar が無い」と誤判定してしまう。
    /// </summary>
    internal static class ModelAvatarResolver
    {
        /// <summary>設定に応じた実効 Avatar。未設定なら null。</summary>
        public static Avatar Resolve(string assetPath, ModelImporter importer)
        {
            return importer.avatarSetup == ModelImporterAvatarSetup.CopyFromOther
                ? importer.sourceAvatar
                : LoadEmbedded(assetPath);
        }

        /// <summary>モデル自身が生成した Avatar サブアセット。</summary>
        public static Avatar LoadEmbedded(string assetPath)
        {
            var direct = AssetDatabase.LoadAssetAtPath<Avatar>(assetPath);
            if (direct != null) return direct;

            return AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath)
                .OfType<Avatar>()
                .FirstOrDefault();
        }
    }
}
#endif
