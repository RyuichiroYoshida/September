#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace September.Editor.HumanoidRig
{
    /// <summary>複数モデルへの一括操作を、進捗表示・アセット編集バッチ・失敗集約付きで実行する。</summary>
    internal static class HumanoidRigBatchRunner
    {
        private const string LogPrefix = "[HumanoidRigFixer]";

        public sealed class Outcome
        {
            public int Succeeded;
            public List<(string assetPath, string error)> Failures { get; } = new List<(string, string)>();
            public List<string> Notes { get; } = new List<string>();
        }

        /// <param name="action">1 モデルに対する処理。戻り値はログに残す補足 (null 可)。</param>
        public static Outcome Run(string title, IReadOnlyList<string> assetPaths, Func<string, string> action)
        {
            var outcome = new Outcome();
            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < assetPaths.Count; i++)
                {
                    string path = assetPaths[i];
                    EditorUtility.DisplayProgressBar(title, path, (float)i / assetPaths.Count);
                    try
                    {
                        string note = action(path);
                        outcome.Succeeded++;
                        if (!string.IsNullOrEmpty(note)) outcome.Notes.Add($"{path}: {note}");
                    }
                    catch (Exception e)
                    {
                        outcome.Failures.Add((path, e.Message));
                        Debug.LogError($"{LogPrefix} {title} 失敗: {path}\n{e}");
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log(Summarize(title, outcome));
            return outcome;
        }

        public static string Summarize(string title, Outcome outcome)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{LogPrefix} {title}: 成功 {outcome.Succeeded} / 失敗 {outcome.Failures.Count}");
            foreach (var note in outcome.Notes) sb.AppendLine("  " + note);
            foreach (var (path, error) in outcome.Failures) sb.AppendLine($"  NG {path}: {error}");
            return sb.ToString().TrimEnd();
        }
    }
}
#endif
