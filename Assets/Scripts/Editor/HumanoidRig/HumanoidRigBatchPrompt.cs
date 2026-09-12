#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;

namespace September.Editor.HumanoidRig
{
    /// <summary>一括操作の確認ダイアログと結果表示。UI と HumanoidRigBatchRunner の橋渡しのみを行う。</summary>
    internal static class HumanoidRigBatchPrompt
    {
        public const string DialogTitle = "Humanoid Rig Fixer";

        /// <summary>確認ダイアログを出してから一括実行する。キャンセルされたら false。</summary>
        public static bool Run(string title, string message, IReadOnlyList<string> targets, Func<string, string> action)
        {
            if (!EditorUtility.DisplayDialog(DialogTitle, message, "実行", "キャンセル")) return false;

            var outcome = HumanoidRigBatchRunner.Run(title, targets, action);
            if (outcome.Failures.Count > 0)
            {
                EditorUtility.DisplayDialog(DialogTitle, HumanoidRigBatchRunner.Summarize(title, outcome), "OK");
            }
            return true;
        }

        public static void Info(string message) => EditorUtility.DisplayDialog(DialogTitle, message, "OK");
    }
}
#endif
