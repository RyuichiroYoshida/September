#if UNITY_EDITOR
using InGame.Common;
using UnityEditor;
using UnityEngine;

namespace September.EditorExtension
{
    /// <summary>
    /// AnimationClipPlayerの歩行・走行クリップを解析し、手入力用の基準速度へ反映するEditorWindow。
    /// </summary>
    public class LocoAnimSpeedCalibrationWindow : EditorWindow
    {
        private const int DefaultSampleRate = 120;

        [SerializeField] private AnimationClipPlayerManager _target;
        [SerializeField] private int _sampleRate = DefaultSampleRate;

        private LocoAnimSpeedEstimate _walkEstimate;
        private LocoAnimSpeedEstimate _runEstimate;
        private bool _hasWalkEstimate;
        private bool _hasRunEstimate;

        /// <summary>足軌跡ベースのロコモーション速度解析ウィンドウを開く。</summary>
        [MenuItem("September/Animation/Locomotion Speed Calibrator")]
        public static void Open()
        {
            var window = GetWindow<LocoAnimSpeedCalibrationWindow>("Loco Speed Calibrator");
            window.minSize = new Vector2(440f, 430f);
            window.TryUseCurrentSelection();
            window.Show();
        }

        private void OnEnable()
        {
            if (_sampleRate <= 0) _sampleRate = DefaultSampleRate;
            if (_target == null) TryUseCurrentSelection();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Locomotion Speed Calibrator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Humanoidの足軌跡を解析し、接地中の足が滑らない1倍速時の移動速度を推定します。" +
                "Root Motionは有効化しません。具体的なキャラクターPrefabを指定してください。",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            _target = (AnimationClipPlayerManager)EditorGUILayout.ObjectField(
                "Target Manager",
                _target,
                typeof(AnimationClipPlayerManager),
                true);
            if (EditorGUI.EndChangeCheck()) ClearResults();

            _sampleRate = EditorGUILayout.IntSlider("Sample Rate", _sampleRate, 30, 240);

            if (!TryResolveTarget(out var clipPlayer, out string targetError))
            {
                EditorGUILayout.HelpBox(targetError, MessageType.Warning);
                return;
            }

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Animator", clipPlayer.Animator, typeof(Animator), true);
                EditorGUILayout.ObjectField("Walk Clip", clipPlayer.WalkClip, typeof(AnimationClip), false);
                EditorGUILayout.ObjectField("Run Clip", clipPlayer.RunClip, typeof(AnimationClip), false);
            }

            EditorGUILayout.Space(8f);
            if (GUILayout.Button("歩行・走行クリップを解析", GUILayout.Height(30f)))
            {
                Analyze(clipPlayer);
            }

            EditorGUILayout.Space(8f);
            DrawEstimate("Walk", _walkEstimate, _hasWalkEstimate);
            DrawEstimate("Run", _runEstimate, _hasRunEstimate);

            bool hasApplicableResult = (_hasWalkEstimate && _walkEstimate.IsValid) ||
                                       (_hasRunEstimate && _runEstimate.IsValid);
            using (new EditorGUI.DisabledScope(!hasApplicableResult))
            {
                if (GUILayout.Button("解析結果を手入力速度へ反映", GUILayout.Height(28f)))
                {
                    ApplyResults();
                }
            }

            if (hasApplicableResult)
            {
                EditorGUILayout.HelpBox(
                    "反映時は Use Clip Average Speed をOFFにします。値はUndoでき、Prefabの保存前に差分を確認できます。",
                    MessageType.None);
            }
        }

        private void Analyze(AnimationClipPlayer clipPlayer)
        {
            ClearResults();

            try
            {
                EditorUtility.DisplayProgressBar("Loco Speed Calibrator", "歩行クリップを解析中...", 0.25f);
                _walkEstimate = LocoAnimSpeedEstimator.Estimate(clipPlayer.Animator, clipPlayer.WalkClip, _sampleRate);
                _hasWalkEstimate = true;

                EditorUtility.DisplayProgressBar("Loco Speed Calibrator", "走行クリップを解析中...", 0.75f);
                _runEstimate = LocoAnimSpeedEstimator.Estimate(clipPlayer.Animator, clipPlayer.RunClip, _sampleRate);
                _hasRunEstimate = true;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Repaint();
        }

        private static void DrawEstimate(string label, LocoAnimSpeedEstimate estimate, bool hasEstimate)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            if (!hasEstimate)
            {
                EditorGUILayout.LabelField("未解析");
                return;
            }

            if (!estimate.IsValid)
            {
                EditorGUILayout.HelpBox(estimate.Error, MessageType.Error);
                return;
            }

            EditorGUILayout.LabelField("推定基準速度", $"{estimate.Speed:F3} m/s");
            EditorGUILayout.LabelField("左足", FormatFootSpeed(estimate.LeftFootSpeed));
            EditorGUILayout.LabelField("右足", FormatFootSpeed(estimate.RightFootSpeed));
            EditorGUILayout.LabelField("信頼度", $"{estimate.Confidence * 100f:F0}%");
            EditorGUILayout.LabelField(
                "接地サンプル",
                $"{estimate.ContactSampleCount} / {estimate.TotalSampleCount}");

            if (estimate.Confidence < 0.5f)
            {
                EditorGUILayout.HelpBox(
                    "左右差または接地サンプル不足により信頼度が低めです。特殊な足運びのクリップは結果を確認してください。",
                    MessageType.Warning);
            }
        }

        private void ApplyResults()
        {
            if (_target == null) return;

            Undo.RecordObject(_target, "Apply Locomotion Animation Speeds");
            var serializedTarget = new SerializedObject(_target);
            serializedTarget.Update();

            var useAverageSpeed = serializedTarget.FindProperty("_useClipAverageSpeed");
            var walkAnimSpeed = serializedTarget.FindProperty("_walkAnimSpeed");
            var runAnimSpeed = serializedTarget.FindProperty("_runAnimSpeed");
            if (useAverageSpeed == null || walkAnimSpeed == null || runAnimSpeed == null)
            {
                Debug.LogError("[LocoAnimSpeedCalibrationWindow] 反映先のSerializedPropertyを取得できません。", _target);
                return;
            }

            useAverageSpeed.boolValue = false;
            if (_hasWalkEstimate && _walkEstimate.IsValid) walkAnimSpeed.floatValue = _walkEstimate.Speed;
            if (_hasRunEstimate && _runEstimate.IsValid) runAnimSpeed.floatValue = _runEstimate.Speed;
            serializedTarget.ApplyModifiedProperties();

            if (PrefabUtility.IsPartOfPrefabInstance(_target))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(_target);
            }

            EditorUtility.SetDirty(_target);
            Debug.Log(
                $"[LocoAnimSpeedCalibrationWindow] {_target.name}へ基準速度を反映しました。" +
                $" Walk={walkAnimSpeed.floatValue:F3}, Run={runAnimSpeed.floatValue:F3}",
                _target);
        }

        private bool TryResolveTarget(out AnimationClipPlayer clipPlayer, out string error)
        {
            clipPlayer = null;
            error = string.Empty;
            if (_target == null)
            {
                error = "AnimationClipPlayerManagerを指定してください。";
                return false;
            }

            var serializedTarget = new SerializedObject(_target);
            var clipPlayerProperty = serializedTarget.FindProperty("_animationClipPlayer");
            clipPlayer = clipPlayerProperty?.objectReferenceValue as AnimationClipPlayer;
            if (clipPlayer == null)
            {
                error = "Target ManagerにAnimationClipPlayerが設定されていません。";
                return false;
            }

            if (clipPlayer.Animator == null)
            {
                error = "AnimationClipPlayerにAnimatorが設定されていません。";
                return false;
            }

            if (clipPlayer.WalkClip == null && clipPlayer.RunClip == null)
            {
                error = "歩行・走行クリップが設定された具体的なキャラクターPrefabを指定してください。";
                return false;
            }

            return true;
        }

        private void TryUseCurrentSelection()
        {
            if (Selection.activeGameObject == null) return;

            _target = Selection.activeGameObject.GetComponent<AnimationClipPlayerManager>();
            if (_target == null)
            {
                _target = Selection.activeGameObject.GetComponentInChildren<AnimationClipPlayerManager>(true);
            }

            if (_target == null)
            {
                _target = Selection.activeGameObject.GetComponentInParent<AnimationClipPlayerManager>();
            }

            ClearResults();
        }

        private void ClearResults()
        {
            _walkEstimate = default;
            _runEstimate = default;
            _hasWalkEstimate = false;
            _hasRunEstimate = false;
        }

        private static string FormatFootSpeed(float speed)
        {
            return speed > 0f ? $"{speed:F3} m/s" : "検出なし";
        }
    }
}
#endif
