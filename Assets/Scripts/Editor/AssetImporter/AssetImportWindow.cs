using UnityEngine;
using UnityEditor;

namespace September.Editor.AssetImporter
{
    public class AssetImportWindow : EditorWindow
    {
        private AssetImportController _controller;
        private ProgressInfo _currentProgress = new() { Progress = 0f, Status = "", Detail = "" };
        private string _statusMessage = AssetImportConstants.Messages.ImportWaiting;
        private bool _isInitialized;
        private Color _defaultLabelColor;
        private float _lastRepaintTime;

        [MenuItem(AssetImportConstants.MenuPath)]
        public static void ShowWindow()
        {
            var window = GetWindow<AssetImportWindow>(AssetImportConstants.WindowTitle);
            window.Show();
        }

        private void OnEnable()
        {
            InitializeController();
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            _controller?.Dispose();
        }

        private void InitializeController()
        {
            _controller = new AssetImportController();
            _controller.OnProgressChanged += OnProgressChanged;
            _controller.OnStatusChanged += OnStatusChanged;

            _ = _controller.InitializeAsync();
        }

        private void OnGUI()
        {
            if (!_isInitialized)
            {
                _defaultLabelColor = GUI.skin.label.normal.textColor;
                _isInitialized = true;
            }

            DrawHeader();
            DrawReleaseSelection();
            DrawImportSection();
            DrawProgressSection();
            DrawDeveloperSection();
            
        }

        private void DrawHeader()
        {
            if ((_controller?.ReleaseNames?.Count ?? 0) == 0)
            {
                DrawColoredLabel(AssetImportConstants.Messages.Initializing, Color.red);
                return;
            }

            DrawColoredLabel(AssetImportConstants.Messages.DoNotPlayScene, Color.red);
        }

        private void DrawReleaseSelection()
        {
            if (_controller?.ReleaseNames?.Count == 0) return;

            var releaseNames = _controller.ReleaseNames?.ToArray() ?? new string[0];
            _controller.SelectedReleaseIndex = EditorGUILayout.Popup(
                "パッケージバージョン",
                _controller.SelectedReleaseIndex,
                releaseNames);

            GUILayout.Space(10);

            if (_controller.SelectedReleaseIndex < releaseNames.Length)
            {
                GUILayout.Label("Selected: " + releaseNames[_controller.SelectedReleaseIndex]);
            }
        }

        private void DrawImportSection()
        {
            GUILayout.Space(10);

            GUI.enabled = !(_controller?.IsImporting ?? false) && (_controller?.ReleaseNames?.Count ?? 0) > 0;

            if (GUILayout.Button("アセットのインポート"))
            {
                if (_controller != null)
                    _ = _controller.ImportSelectedAssetAsync();
            }

            GUI.enabled = true;

            GUILayout.Label(_statusMessage);
        }

        private void DrawProgressSection()
        {
            if (_controller?.IsImporting != true) return;

            GUILayout.Space(10);
            
            // ステータス表示
            if (!string.IsNullOrEmpty(_currentProgress.Status))
            {
                DrawColoredLabel(_currentProgress.Status, Color.cyan);
            }

            // プログレスバー
            var rect = GUILayoutUtility.GetRect(0, 20, GUILayout.ExpandWidth(true));
            var progressValue = Mathf.Clamp01(_currentProgress.Progress);
            var progressText = !string.IsNullOrEmpty(_currentProgress.Detail) 
                ? _currentProgress.Detail 
                : $"進捗: {progressValue * 100:F1}%";
            
            EditorGUI.ProgressBar(rect, progressValue, progressText);

            GUILayout.Space(5);
        }

        private void DrawDeveloperSection()
        {
            GUILayout.Space(50);
            GUILayout.Label(AssetImportConstants.Messages.ProgrammerSection);

            if (GUILayout.Button("アセット一覧の同期"))
            {
                _ = _controller.InitializeAsync();
            }

            GUILayout.Space(10);
            

            // 設定UI
            var newShowImportWindow = GUILayout.Toggle(
                AssetImportSettings.ShowImportWindow,
                "アセットの手動インポート");

            if (newShowImportWindow != AssetImportSettings.ShowImportWindow)
            {
                AssetImportSettings.ShowImportWindow = newShowImportWindow;
            }

            EditorGUILayout.LabelField(AssetImportSettings.ShowImportWindow
                ? "現在、アセットインポート時にインポートするフォルダを選べます"
                : "現在、自動的に全てのアセットの中身がインポートされます");

            GUILayout.Space(10);

            var newFileChecking = GUILayout.Toggle(
                AssetImportSettings.FileChecking,
                "ファイルの存在確認");

            if (newFileChecking != AssetImportSettings.FileChecking)
            {
                AssetImportSettings.FileChecking = newFileChecking;
            }

            GUILayout.Space(10);

            var newUseLogger = GUILayout.Toggle(
                AssetImportSettings.UseLogger,
                "Log出力を有効化");

            if (newUseLogger != AssetImportSettings.UseLogger)
            {
                AssetImportSettings.UseLogger = newUseLogger;
                // ログ設定変更時は明示的に通知
                OnSettingsChanged();
            }
        }

        private void DrawColoredLabel(string text, Color color)
        {
            var style = GUI.skin.label;
            var originalColor = style.normal.textColor;

            style.normal.textColor = color;
            GUILayout.Label(text, style);
            style.normal.textColor = originalColor;
        }

        private void OnProgressChanged(ProgressInfo progressInfo)
        {
            _currentProgress = progressInfo;
            
            // メインスレッドで安全にRepaintを実行
            if (EditorApplication.isPlaying)
            {
                EditorApplication.delayCall += Repaint;
            }
            else
            {
                Repaint();
            }
        }

        private void OnStatusChanged(string status)
        {
            _statusMessage = status;
            
            // メインスレッドで安全にRepaintを実行
            if (EditorApplication.isPlaying)
            {
                EditorApplication.delayCall += Repaint;
            }
            else
            {
                Repaint();
            }
        }

        private void OnEditorUpdate()
        {
            // ダウンロード中は定期的にRepaintを呼び出してUIをスムーズに更新
            if (_controller?.IsImporting == true)
            {
                // フレームレート制限でパフォーマンスを向上（10FPS制限）
                if (Time.realtimeSinceStartup - _lastRepaintTime > 0.1f)
                {
                    Repaint();
                    _lastRepaintTime = Time.realtimeSinceStartup;
                }
            }
        }

        private void OnSettingsChanged()
        {
            // 設定が変更された場合、コントローラーに通知する必要があるかもしれませんが、
            // 現在の実装では次回の操作時（InitializeAsync/ImportSelectedAssetAsync）に
            // 自動的にCheckAndUpdateSettings()が呼ばれるため、特別な処理は不要
        }
    }
}