#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace September.Editor.HumanoidRig
{
    /// <summary>
    /// 「整合性チェック」タブ。基準モデルを 1 つ指定し、対象フォルダのアニメーション FBX が
    /// そのスケルトンと整合しているか (ボーン欠落 / 階層差 / カーブ解決不可) を検査する。
    /// </summary>
    internal sealed class ModelConsistencySection
    {
        private readonly HumanoidRigTargetFolders _folders;
        private readonly ModelPathSelectionList _list = new ModelPathSelectionList();
        private readonly Dictionary<string, ModelConsistencyReport> _reports = new Dictionary<string, ModelConsistencyReport>(StringComparer.Ordinal);
        private GameObject _reference;
        private bool _scanned;

        public ModelConsistencySection(HumanoidRigTargetFolders folders)
        {
            _folders = folders;
        }

        public void Draw()
        {
            EditorGUILayout.HelpBox(
                "基準モデルのボーン階層に対して、アニメーション FBX が整合しているかを検査します。\n" +
                "アセットは変更しません。",
                MessageType.Info);

            _reference = (GameObject)EditorGUILayout.ObjectField("基準モデル (FBX)", _reference, typeof(GameObject), false);

            DrawScanBar();
            EditorGUILayout.Space();
            _list.Draw("結果", GetRowInfo, _scanned ? "対象のモデルがありません。" : "スキャンを実行してください。");
            EditorGUILayout.Space();
            DrawActions();
        }

        private void DrawScanBar()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("対象をスキャン", GUILayout.Width(140f), GUILayout.Height(24f))) Scan();
                GUILayout.FlexibleSpace();
                int errors = _reports.Values.Count(r => r.HasError);
                GUILayout.Label($"対象 {_list.Count} 件 / 判定済 {_reports.Count} 件 / 不整合 {errors} 件");
            }
        }

        private void Scan()
        {
            _reports.Clear();
            string referencePath = ReferencePath();
            _list.SetPaths(_folders.FindModelPaths()
                .Where(p => !string.Equals(p, referencePath, StringComparison.OrdinalIgnoreCase)));
            _scanned = true;
        }

        private string ReferencePath() => _reference == null ? null : AssetDatabase.GetAssetPath(_reference);

        private ModelRowInfo GetRowInfo(string path)
        {
            if (!_reports.TryGetValue(path, out var report)) return ModelRowInfo.Plain("未判定");

            var color = report.HasError ? Color.red : report.HasWarning ? new Color(0.9f, 0.6f, 0f) : Color.green;
            return new ModelRowInfo(report.StatusLabel, color, report.Summary);
        }

        private void DrawActions()
        {
            using (new EditorGUI.DisabledScope(_reference == null || _list.SelectedCount == 0))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button($"選択中 {_list.SelectedCount} 件を検査")) Check();
                    using (new EditorGUI.DisabledScope(_reports.Count == 0))
                    {
                        if (GUILayout.Button("結果をログ出力", GUILayout.Width(140f))) LogReports();
                    }
                }
            }

            if (_reference == null)
            {
                EditorGUILayout.HelpBox("基準モデルを指定してください。", MessageType.Warning);
            }
        }

        private void Check()
        {
            var reference = _reference;
            string referencePath = ReferencePath();
            var targets = _list.Selected;

            try
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    EditorUtility.DisplayProgressBar("整合性チェック", targets[i], (float)i / Math.Max(1, targets.Count));
                    try
                    {
                        _reports[targets[i]] = ModelConsistencyChecker.Check(reference, referencePath, targets[i]);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[HumanoidRigFixer] 整合性チェック失敗: {targets[i]}\n{e}");
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Debug.Log($"[HumanoidRigFixer] 整合性チェック完了: {targets.Count} 件中 不整合 {_reports.Values.Count(r => r.HasError)} 件");
        }

        private void LogReports()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[HumanoidRigFixer] 整合性チェック結果 (基準: {ReferencePath()})");
            foreach (var report in _reports.Values.OrderBy(r => r.AssetPath, StringComparer.OrdinalIgnoreCase))
            {
                sb.AppendLine($"[{report.StatusLabel}] {report.AssetPath}");
                foreach (var line in report.Summary.Split('\n')) sb.AppendLine("  " + line);
            }
            Debug.Log(sb.ToString().TrimEnd());
        }
    }
}
#endif
