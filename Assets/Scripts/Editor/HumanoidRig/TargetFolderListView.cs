#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace September.Editor.HumanoidRig
{
    /// <summary>検査対象フォルダの一覧と追加/削除 UI。全タブで共有する。</summary>
    internal sealed class TargetFolderListView
    {
        private readonly HumanoidRigTargetFolders _folders;

        public TargetFolderListView(HumanoidRigTargetFolders folders)
        {
            _folders = folders;
        }

        public void Draw()
        {
            EditorGUILayout.LabelField("対象フォルダ", EditorStyles.boldLabel);

            int removeIndex = -1;
            for (int i = 0; i < _folders.Folders.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool exists = AssetDatabase.IsValidFolder(_folders.Folders[i]);
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.TextField(_folders.Folders[i]);
                    }
                    GUILayout.Label(exists ? string.Empty : "(存在しない)", GUILayout.Width(80f));
                    if (GUILayout.Button("×", GUILayout.Width(24f))) removeIndex = i;
                }
            }
            if (removeIndex >= 0)
            {
                _folders.Folders.RemoveAt(removeIndex);
                _folders.Save();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("フォルダを追加...", GUILayout.Width(140f))) AddFromPanel();
                if (GUILayout.Button("Project 選択中のフォルダを追加", GUILayout.Width(220f))) AddFromSelection();
            }
        }

        private void AddFromPanel()
        {
            string absolute = EditorUtility.OpenFolderPanel("対象フォルダを選択", Application.dataPath, string.Empty);
            if (string.IsNullOrEmpty(absolute)) return;

            string relative = HumanoidRigTargetFolders.ToProjectRelative(absolute);
            if (relative == null)
            {
                HumanoidRigBatchPrompt.Info("Assets フォルダ配下のフォルダを選択してください。");
                return;
            }
            Add(relative);
        }

        private void AddFromSelection()
        {
            var folders = Selection.assetGUIDs
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(AssetDatabase.IsValidFolder)
                .ToList();
            if (folders.Count == 0)
            {
                HumanoidRigBatchPrompt.Info("Project ビューでフォルダを選択してから実行してください。");
                return;
            }
            foreach (var folder in folders) Add(folder);
        }

        private void Add(string projectRelative)
        {
            if (_folders.Folders.Contains(projectRelative)) return;
            _folders.Folders.Add(projectRelative);
            _folders.Save();
        }
    }
}
#endif
