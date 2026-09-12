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
        private Avatar _avatar;
        private AvatarMask _mask;
        private bool _applyAvatar = true;
        private bool _applyMask = true;
        private bool _clearMask;
        private bool _applyNode = true;
        private bool _scanned;

        public RootMotionNodeSection(HumanoidRigTargetFolders folders) => _folders = folders;

        public void Draw()
        {
            EditorGUILayout.HelpBox(
                "Humanoid Avatar・AvatarMask・Root Motion Node をまとめて設定します。チェックした項目だけ変更します。\n" +
                "設定はモデル単位で全クリップに適用されます（単独の .anim は対象外）。\n" +
                "基準 FBX を指定し、その階層から Root Motion Node を選択してください。",
                MessageType.Info);
            DrawNodeSelector();
            _applyAvatar = EditorGUILayout.ToggleLeft("Humanoid Avatar を設定（Copy From Other Avatar）", _applyAvatar);
            using (new EditorGUI.DisabledScope(!_applyAvatar))
                _avatar = (Avatar)EditorGUILayout.ObjectField("コピー元 Avatar", _avatar, typeof(Avatar), false);
            _applyMask = EditorGUILayout.ToggleLeft("Animation の AvatarMask を設定", _applyMask);
            using (new EditorGUI.DisabledScope(!_applyMask))
            {
                _clearMask = EditorGUILayout.Toggle("マスクを解除", _clearMask);
                using (new EditorGUI.DisabledScope(_clearMask))
                    _mask = (AvatarMask)EditorGUILayout.ObjectField("AvatarMask", _mask, typeof(AvatarMask), false);
            }
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

            _list.Draw("現在の Avatar / Mask / Root Motion Node",
                path => ModelRowInfo.Plain(_descriptions.TryGetValue(path, out var info) ? info : string.Empty),
                _scanned ? "対象のアニメーションモデルがありません。" : "フォルダをスキャン、または Project で FBX / 内包クリップを選択して読み込んでください。");

            string error = SettingsError();
            if (error != null) EditorGUILayout.HelpBox(error, MessageType.Info);
            using (new EditorGUI.DisabledScope(_list.SelectedCount == 0 || error != null))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button($"選択中 {_list.SelectedCount} 件にまとめて適用")) Run();
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
                    _avatar = importer == null ? null : ModelAvatarResolver.Resolve(importer.assetPath, importer);
                }
            }

            _applyNode = EditorGUILayout.ToggleLeft("Root Motion Node を設定", _applyNode);
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
            using (new EditorGUI.DisabledScope(!_applyNode))
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
                    EditorUtility.DisplayProgressBar("アニメーション一括設定: スキャン中", path, (float)i / Math.Max(1, paths.Count));
                    var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                    if (importer == null || !importer.importAnimation) continue;
                    int count = AnimationAvatarMaskApplier.CountClips(path);
                    if (count == 0) continue;
                    animated.Add(path);
                    string node = string.IsNullOrEmpty(importer.motionNodeName) ? "None" : importer.motionNodeName;
                    var avatar = ModelAvatarResolver.Resolve(path, importer);
                    _descriptions[path] = $"{importer.animationType} / {importer.avatarSetup} / Avatar: {(avatar == null ? "なし" : avatar.name)}\n" +
                        $"{AnimationAvatarMaskApplier.DescribeMask(path)} / Root Motion Node: {node}";
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

        private string SettingsError()
        {
            if (!_applyAvatar && !_applyMask && !_applyNode) return "変更する項目をチェックしてください。";
            if (_applyAvatar && (_avatar == null || !_avatar.isValid || !_avatar.isHuman)) return "有効な Humanoid Avatar を指定してください。";
            if (_applyMask && !_clearMask && _mask == null) return "AvatarMask を指定するか、マスクを解除を選択してください。";
            if (_applyNode && _referenceModel == null) return "Root Motion Node の基準 FBX を指定してください。";
            return null;
        }

        private void Run()
        {
            string node = _node;
            var changes = new List<string>();
            if (_applyAvatar) changes.Add($"Humanoid Avatar: {_avatar.name} (Copy From Other Avatar)");
            if (_applyMask) changes.Add(_clearMask ? "AvatarMask: 解除" : $"AvatarMask: {_mask.name}");
            if (_applyNode) changes.Add($"Root Motion Node: {(string.IsNullOrEmpty(node) ? "<None>" : node)}");
            // Project 選択から読み込んだ対象も維持して表示を更新する。
            var paths = new List<string>(_descriptions.Keys);
            if (HumanoidRigBatchPrompt.Run("アニメーション一括設定",
                $"選択中 {_list.SelectedCount} 件に以下を適用し再インポートします。\n{string.Join("\n", changes)}\n各モデルの全クリップに適用されます。",
                _list.Selected, ApplySettings))
                Scan(paths);
        }

        private string ApplySettings(string path)
        {
            // 全項目の検証を済ませてから設定を書き換え、モデルごとに一度だけ再インポートする。
            string error = SettingsError();
            if (error != null) throw new InvalidOperationException(error);
            var importer = ModelReimporter.RequireImporter(path);
            if (!importer.importAnimation) throw new InvalidOperationException("アニメーションが無効です。");
            if (_applyAvatar && string.Equals(AssetDatabase.GetAssetPath(_avatar), path, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("コピー元 Avatar 自身のモデルには適用できません。対象から外してください。");
            string resolved = _applyNode && !string.IsNullOrEmpty(_node)
                ? AnimationRootMotionNodeApplier.ResolvePath(importer.transformPaths, _node) : string.Empty;
            var clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0) throw new InvalidOperationException("アニメーションクリップがありません。");

            if (_applyAvatar)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                importer.sourceAvatar = _avatar;
            }
            if (_applyMask)
            {
                foreach (var clip in clips)
                {
                    clip.maskType = _clearMask ? ClipAnimationMaskType.None : ClipAnimationMaskType.CopyFromOther;
                    clip.maskSource = _clearMask ? null : _mask;
                }
                importer.clipAnimations = clips;
            }
            if (_applyNode) importer.motionNodeName = resolved;
            ModelReimporter.Apply(importer);
            return $"{clips.Length} クリップ / 選択した設定を適用";
        }
    }
}
#endif
