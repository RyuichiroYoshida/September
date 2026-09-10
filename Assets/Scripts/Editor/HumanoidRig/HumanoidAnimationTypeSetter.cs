#if UNITY_EDITOR
using UnityEditor;

namespace September.Editor.HumanoidRig
{
    /// <summary>FBX の Animation Type を Humanoid (Avatar は自モデルから生成) に切り替える。</summary>
    internal static class HumanoidAnimationTypeSetter
    {
        /// <returns>設定を変更して再インポートしたら true。既に Humanoid なら false。</returns>
        public static bool EnsureHumanoid(string assetPath)
        {
            var importer = ModelReimporter.RequireImporter(assetPath);
            bool changed = false;

            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                changed = true;
            }
            if (importer.avatarSetup == ModelImporterAvatarSetup.NoAvatar)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                changed = true;
            }

            if (changed) ModelReimporter.Apply(importer);
            return changed;
        }
    }
}
#endif
