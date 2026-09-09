#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace September.Editor.HumanoidRig
{
    /// <summary>
    /// 検査対象フォルダの永続化 (EditorPrefs、プロジェクト別) と、フォルダ配下のモデルアセット列挙。
    /// </summary>
    internal sealed class HumanoidRigTargetFolders
    {
        private const string PrefsKeyPrefix = "September.HumanoidRigFixer.Folders.";
        private const char Separator = '\n';

        /// <summary>RigHumanoidBatchPredefined と同じ既定フォルダ。存在しないものは列挙時に無視される。</summary>
        private static readonly string[] DefaultFolders =
        {
            "Assets/Model/HARU",
            "Assets/Model/OKB",
            "Assets/Model/TANIHIRA",
            "Assets/Model/KOINUMA",
        };

        public List<string> Folders { get; } = new List<string>();

        public static HumanoidRigTargetFolders Load()
        {
            var target = new HumanoidRigTargetFolders();
            string saved = EditorPrefs.GetString(PrefsKey(), string.Empty);
            var folders = saved.Split(new[] { Separator }, StringSplitOptions.RemoveEmptyEntries);
            target.Folders.AddRange(folders.Length > 0 ? folders : DefaultFolders);
            return target;
        }

        public void Save()
        {
            EditorPrefs.SetString(PrefsKey(), string.Join(Separator.ToString(), Folders));
        }

        public IReadOnlyList<string> ValidFolders => Folders.Where(AssetDatabase.IsValidFolder).ToList();

        public IReadOnlyList<string> InvalidFolders => Folders.Where(f => !AssetDatabase.IsValidFolder(f)).ToList();

        /// <summary>有効フォルダ配下の ModelImporter を持つアセット (FBX 等) のパスを列挙する。</summary>
        public IReadOnlyList<string> FindModelPaths()
        {
            var valid = ValidFolders;
            if (valid.Count == 0) return Array.Empty<string>();

            return AssetDatabase.FindAssets("t:Model", valid.ToArray())
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .Where(p => AssetImporter.GetAtPath(p) is ModelImporter)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>絶対パスを "Assets/..." 形式に変換する。プロジェクト外なら null。</summary>
        public static string ToProjectRelative(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath)) return null;
            string normalized = absolutePath.Replace('\\', '/');
            string dataPath = Application.dataPath.Replace('\\', '/');
            if (!normalized.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase)) return null;
            return "Assets" + normalized.Substring(dataPath.Length);
        }

        // EditorPrefs はユーザ単位で共有されるため、プロジェクトパスのハッシュでキーを分ける。
        private static string PrefsKey() => PrefsKeyPrefix + Fnv1a(Application.dataPath);

        private static string Fnv1a(string s)
        {
            const uint offset = 2166136261;
            const uint prime = 16777619;
            uint hash = offset;
            foreach (char c in s)
            {
                hash ^= c;
                hash *= prime;
            }
            return hash.ToString("x8");
        }
    }
}
#endif
