#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace September.Editor.HumanoidRig
{
    internal sealed class RootMotionNodeSection
    {
        private readonly HumanoidRigTargetFolders _folders;
        private readonly ModelPathSelectionList _list = new ModelPathSelectionList();
        private readonly Dictionary<string, string> _descriptions = new Dictionary<string, string>(StringComparer.Ordinal);
        private string _node = string.Empty;
        private GameObject _referenceModel;
        private bool _scanned;

        public RootMotionNodeSection(HumanoidRigTargetFolders folders) => _folders = folders;

        public void Draw()
        {
            EditorGUILayout.HelpBox(
                "選択したアニメーションモデルの Root Motion Node を変更し、再インポートします。\n" +
                "設定はモデル単位で全クリップに適用されます（単独の .anim は対象外）。\n" +
                "基準 FBX を指定し、その階層から Root Motion Node を選択してください。",
                MessageType.Info);
            DrawNodeSelector();
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("アニメーションをスキャン")) Scan(_folders.FindModelPaths());
                if (GUILayout.Button("Project 選択から読み込み"))
                {
                    var paths = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var asset in Selection.objects)
                    {
                        string path = AssetDatabase.GetAssetPath(asset);
                        if (!string.IsNullOrEmpty(path)) paths.Add(path);
                    }
                    Scan(new List<string>(paths));
                }
                GUILayout.Label($"対象 {_list.Count} 件");
            }

            _list.Draw("クリップ / 現在の Root Motion Node",
                path => ModelRowInfo.Plain(_descriptions.TryGetValue(path, out var info) ? info : string.Empty),
                _scanned ? "対象のアニメーションモデルがありません。" : "フォルダをスキャン、または Project で FBX / 内包クリップを選択して読み込んでください。");

            using (new EditorGUI.DisabledScope(_list.SelectedCount == 0 || _referenceModel == null))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button($"選択中 {_list.SelectedCount} 件に適用")) Run(string.IsNullOrEmpty(_node));
            }
        }

        private void DrawNodeSelector()
        {
            using (var change = new EditorGUI.ChangeCheckScope())
            {
                var reference = (GameObject)EditorGUILayout.ObjectField("基準 FBX", _referenceModel, typeof(GameObject), false);
                if (change.changed)
                {
                    var importer = reference == null ? null : AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(reference)) as ModelImporter;
                    _referenceModel = importer == null ? null : AssetDatabase.LoadAssetAtPath<GameObject>(importer.assetPath);
                    _node = importer == null ? string.Empty : importer.motionNodeName;
                }
            }

            var source = _referenceModel == null ? null : AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(_referenceModel)) as ModelImporter;
            if (source == null)
            {
                EditorGUILayout.HelpBox("ノード候補を読み込む基準 FBX を指定してください。", MessageType.Info);
                return;
            }

            // Animation インポーターと同じ並び。先頭の Transform は専用の選択肢で表す。
            var paths = source.transformPaths ?? Array.Empty<string>();
            var options = new List<string> { "<None>" };
            if (paths.Length > 0) options.Add(AnimationRootMotionNodeApplier.RootTransform);
            for (int i = 1; i < paths.Length; i++) options.Add(paths[i]);
            int selected = string.IsNullOrEmpty(_node) ? 0 : options.IndexOf(_node);
            if (selected < 0)
            {
                _node = string.Empty;
                selected = 0;
            }
            selected = EditorGUILayout.Popup("Root Motion Node", selected, options.ToArray());
            _node = selected == 0 ? string.Empty : options[selected];
        }

        private void Scan(IReadOnlyList<string> paths)
        {
            var animated = new List<string>();
            _descriptions.Clear();
            try
            {
                for (int i = 0; i < paths.Count; i++)
                {
                    string path = paths[i];
                    EditorUtility.DisplayProgressBar("Root Motion Node: スキャン中", path, (float)i / Math.Max(1, paths.Count));
                    var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                    if (importer == null || !importer.importAnimation) continue;
                    int count = AnimationAvatarMaskApplier.CountClips(path);
                    if (count == 0) continue;
                    animated.Add(path);
                    string node = string.IsNullOrEmpty(importer.motionNodeName) ? "None" : importer.motionNodeName;
                    _descriptions[path] = $"クリップ {count} 件 / {node}";
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            animated.Sort(StringComparer.OrdinalIgnoreCase);
            _list.SetPaths(animated);
            _scanned = true;
        }

        private void Run(bool clear)
        {
            string node = _node;
            string label = clear ? "None" : node;
            // Project 選択から読み込んだ対象も維持して表示を更新する。
            var paths = new List<string>(_descriptions.Keys);
            if (HumanoidRigBatchPrompt.Run("Root Motion Node 一括変更",
                $"選択中 {_list.SelectedCount} 件の Root Motion Node を「{label}」に変更し再インポートします。\n各モデルの全クリップに適用されます。",
                _list.Selected, path => AnimationRootMotionNodeApplier.Apply(path, node, clear)))
                Scan(paths);
        }
    }
}
#endif
