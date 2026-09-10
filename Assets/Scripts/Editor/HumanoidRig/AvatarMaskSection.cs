#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace September.Editor.HumanoidRig
{
    /// <summary>
    /// 「AvatarMask 一括適用」タブ。対象フォルダからアニメーションを持つ FBX を集め、
    /// 選択したモデルの全クリップに 1 つの AvatarMask を割り当てる / 解除する。
    /// </summary>
    internal sealed class AvatarMaskSection
    {
        private readonly HumanoidRigTargetFolders _folders;
        private readonly ModelPathSelectionList _list = new ModelPathSelectionList();
        private readonly Dictionary<string, string> _descriptions = new Dictionary<string, string>(StringComparer.Ordinal);
        private AvatarMask _mask;
        private bool _scanned;

        public AvatarMaskSection(HumanoidRigTargetFolders folders)
        {
            _folders = folders;
        }

        public void Draw()
        {
            EditorGUILayout.HelpBox(
                "アニメーションクリップを持つ FBX の全クリップに、同じ AvatarMask を割り当てます。\n" +
                "インポート設定 (Animation タブの Mask) を書き換えて再インポートします。",
                MessageType.Info);

            DrawScanBar();
            EditorGUILayout.Space();
            _list.Draw("クリップ / 現在のマスク", GetRowInfo, _scanned ? "アニメーションを持つ FBX が見つかりません。" : "スキャンを実行してください。");
            EditorGUILayout.Space();
            DrawActions();
        }

        private void DrawScanBar()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("アニメーション FBX をスキャン", GUILayout.Width(220f), GUILayout.Height(24f))) Scan();
                GUILayout.FlexibleSpace();
                GUILayout.Label($"対象 {_list.Count} 件");
            }
        }

        private void Scan()
        {
            _descriptions.Clear();
            var animated = new List<string>();

            var paths = _folders.FindModelPaths();
            try
            {
                for (int i = 0; i < paths.Count; i++)
                {
                    EditorUtility.DisplayProgressBar("AvatarMask 一括適用: スキャン中", paths[i], (float)i / Math.Max(1, paths.Count));
                    if (AnimationAvatarMaskApplier.CountClips(paths[i]) == 0) continue;

                    animated.Add(paths[i]);
                    _descriptions[paths[i]] = AnimationAvatarMaskApplier.DescribeMask(paths[i]);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            _list.SetPaths(animated);
            _scanned = true;
            Debug.Log($"[HumanoidRigFixer] アニメーション FBX {animated.Count} 件 (走査 {paths.Count} 件)");
        }

        private ModelRowInfo GetRowInfo(string path)
        {
            return ModelRowInfo.Plain(_descriptions.TryGetValue(path, out var description) ? description : string.Empty);
        }

        private void DrawActions()
        {
            _mask = (AvatarMask)EditorGUILayout.ObjectField("適用する AvatarMask", _mask, typeof(AvatarMask), false);

            using (new EditorGUI.DisabledScope(_list.SelectedCount == 0))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(_mask == null))
                    {
                        if (GUILayout.Button($"選択中 {_list.SelectedCount} 件に AvatarMask を適用"))
                        {
                            var mask = _mask;
                            Run("AvatarMask 適用", $"AvatarMask \"{(mask == null ? string.Empty : mask.name)}\" を選択中 {_list.SelectedCount} 件の全クリップに適用し再インポートします。",
                                p => $"{AnimationAvatarMaskApplier.Apply(p, mask)} クリップ");
                        }
                    }
                    if (GUILayout.Button("マスクを解除", GUILayout.Width(140f)))
                    {
                        Run("AvatarMask 解除", $"選択中 {_list.SelectedCount} 件の全クリップのマスク設定を解除し再インポートします。",
                            p => $"{AnimationAvatarMaskApplier.Clear(p)} クリップ");
                    }
                }
            }
        }

        private void Run(string title, string message, Func<string, string> action)
        {
            if (!HumanoidRigBatchPrompt.Run(title, message, _list.Selected, action)) return;
            Scan();
        }
    }
}
#endif
