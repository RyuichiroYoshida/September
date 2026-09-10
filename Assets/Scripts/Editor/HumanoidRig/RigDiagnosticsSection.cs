#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace September.Editor.HumanoidRig
{
    /// <summary>
    /// 「リグ検査/修正」タブ。対象フォルダをスキャンして問題モデルを一覧し、
    /// 選択モデルへ Humanoid 化 / ボーン再割当 / T-Pose 強制 / Avatar コピーを適用する。
    /// </summary>
    internal sealed class RigDiagnosticsSection
    {
        private readonly HumanoidRigTargetFolders _folders;
        private readonly ModelPathSelectionList _list = new ModelPathSelectionList();
        private readonly Dictionary<string, HumanoidRigReport> _reports = new Dictionary<string, HumanoidRigReport>(StringComparer.Ordinal);
        private Avatar _sourceAvatar;
        private bool _showOnlyProblems = true;

        public RigDiagnosticsSection(HumanoidRigTargetFolders folders)
        {
            _folders = folders;
        }

        public void Draw()
        {
            DrawScanBar();
            EditorGUILayout.Space();
            _list.Draw("問題", GetRowInfo, _reports.Count == 0 ? "スキャンを実行してください。" : "表示対象のモデルはありません。");
            EditorGUILayout.Space();
            DrawActions();
        }

        private void DrawScanBar()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("スキャン", GUILayout.Width(120f), GUILayout.Height(24f))) Scan();

                bool showOnlyProblems = GUILayout.Toggle(_showOnlyProblems, "問題のあるモデルのみ表示");
                if (showOnlyProblems != _showOnlyProblems)
                {
                    _showOnlyProblems = showOnlyProblems;
                    RefreshList();
                }

                GUILayout.FlexibleSpace();
                GUILayout.Label($"モデル {_reports.Count} 件 / 問題 {_reports.Values.Count(r => r.HasProblem)} 件");
            }
        }

        private void Scan()
        {
            _reports.Clear();

            var paths = _folders.FindModelPaths();
            try
            {
                for (int i = 0; i < paths.Count; i++)
                {
                    EditorUtility.DisplayProgressBar("Humanoid Rig Fixer: スキャン中", paths[i], (float)i / Math.Max(1, paths.Count));
                    _reports[paths[i]] = HumanoidRigDiagnoser.Diagnose(paths[i]);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            foreach (var missing in _folders.InvalidFolders)
            {
                Debug.LogWarning($"[HumanoidRigFixer] フォルダが存在しません: {missing}");
            }
            Debug.Log($"[HumanoidRigFixer] スキャン完了: {_reports.Count} 件中 問題 {_reports.Values.Count(r => r.HasProblem)} 件");
            RefreshList();
        }

        private void RefreshList()
        {
            _list.SetPaths(_reports
                .Where(pair => !_showOnlyProblems || pair.Value.HasProblem)
                .Select(pair => pair.Key)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase));
        }

        private ModelRowInfo GetRowInfo(string path)
        {
            if (!_reports.TryGetValue(path, out var report)) return ModelRowInfo.Plain(string.Empty);

            var color = report.HasError ? Color.red : report.HasProblem ? new Color(0.9f, 0.6f, 0f) : Color.green;
            return new ModelRowInfo(report.StatusLabel, color, report.HasProblem ? report.IssueSummary : report.AvatarSetup.ToString());
        }

        private void DrawActions()
        {
            EditorGUILayout.LabelField($"選択中 {_list.SelectedCount} 件への操作", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(_list.SelectedCount == 0))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Humanoid 化"))
                    {
                        Run("Humanoid 化", p => HumanoidAnimationTypeSetter.EnsureHumanoid(p) ? "変更" : "既に Humanoid");
                    }
                    if (GUILayout.Button("命名規則でボーン再割当"))
                    {
                        Run("命名規則でボーン再割当", p => HumanoidBoneMappingApplier.ApplyByName(p).Summarize());
                    }
                    if (GUILayout.Button("Unity 自動割当に戻す"))
                    {
                        Run("Unity 自動割当に戻す", p => { HumanoidBoneMappingApplier.ResetToAutoMapping(p); return null; });
                    }
                    if (GUILayout.Button("T-Pose 強制"))
                    {
                        Run("T-Pose 強制", p => HumanoidTPoseEnforcer.Enforce(p) == TPoseResult.Fixed ? "補正" : "既に有効");
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    _sourceAvatar = (Avatar)EditorGUILayout.ObjectField("コピー元 Avatar", _sourceAvatar, typeof(Avatar), false);
                    using (new EditorGUI.DisabledScope(_sourceAvatar == null))
                    {
                        if (GUILayout.Button("Avatar をコピーして統一", GUILayout.Width(180f)))
                        {
                            var source = _sourceAvatar;
                            Run("Avatar コピー", p => { AvatarSourceCopier.Apply(p, source); return null; });
                        }
                    }
                }
            }
        }

        private void Run(string title, Func<string, string> action)
        {
            var targets = _list.Selected;
            string message = $"{title} を {targets.Count} 件のモデルに適用し再インポートします。\n(.meta のインポート設定が書き換わります)";
            if (!HumanoidRigBatchPrompt.Run(title, message, targets, action)) return;
            Scan();
        }
    }
}
#endif
