using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEditor;

public class EditorButton : EditorWindow
{
    private readonly CancellationTokenSource _cts = new();
    private CancellationToken _ct;

    private string _syncWaitingText = "リソース読み込み中";
    private string _syncImportingText = "インポート実行待機中";

    private int _releasesSelectedIndex;
    private List<string> _releasesList = new();

    private bool _showImportWindow;
    private bool _isInitialized = true;
    private Color _defaultLabelColor;

    private AssetsImporter _importer;

    private bool _isFileChecking;
    // まめちしき
    // Unityには UnityEditor.AssetImporter というテクスチャ等のアセットを自動でインポートするやつがあるらしいわよ
    // https://light11.hatenadiary.com/entry/2018/04/05/194303
    // https://light11.hatenadiary.com/entry/2018/04/05/194529

    [MenuItem("September/Import")]
    public static void ShowWindow()
    {
        var window = GetWindow<EditorButton>("ImportWindow");
        window.Show();
    }

    private void OnGUI()
    {
        if (_isInitialized)
        {
            // Labelの色を上書きしてしまうため、元に戻す用でデフォルトカラーを保存しておく
            _defaultLabelColor = GUI.skin.label.normal.textColor;
        }

        _isInitialized = false;

        if (_releasesList.Count == 0)
        {
            DrawColorLabel("初期化中...", Color.red);
            return;
        }

        DrawColorLabel("インポートが終わるまでUnityのシーンを再生しないでください！", Color.red);

        // UnityPackageをインポートするときのファイル選択ウィンドウを表示するかのフラグ (デフォルトは表示しない false)
        _showImportWindow = EditorGUILayout.Toggle("アセットの手動インポート", _showImportWindow);

        // 状態を表示
        EditorGUILayout.LabelField(_showImportWindow
            ? "現在、アセットインポート時にインポートするフォルダを選べます"
            : "現在、自動的に全てのアセットの中身がインポートされます");

        _releasesSelectedIndex = EditorGUILayout.Popup("パッケージバージョン", _releasesSelectedIndex, _releasesList.ToArray());

        GUILayout.Space(10);
        GUILayout.Label("Selected: " + _releasesList[_releasesSelectedIndex]);

        GUILayout.Space(10);

        if (GUILayout.Button("アセットのインポート"))
        {
            _syncImportingText = "アセットインポート中";
            _ = Import(result => _syncImportingText = result);
        }

        GUILayout.Label(_syncImportingText);

        GUILayout.Space(10);

        if (GUILayout.Button("アセット一覧の同期"))
        {
            _syncWaitingText = "リソース読み込み中";
            _ = Sync(result => _syncWaitingText = result);
        }

        GUILayout.Label(_syncWaitingText);

        GUILayout.Space(50);
        GUILayout.Label("-----------------これより下はプログラマー用-----------------");
        _isFileChecking = GUILayout.Toggle(_isFileChecking, "ファイルの存在確認");
    }

    private void OnEnable()
    {
        _ct = _cts.Token;

        _ = Sync(result => _syncWaitingText = result);
    }

    private void OnDisable()
    {
        _cts.Cancel();
        _importer.Dispose();
    }

    private async UniTaskVoid Sync(Action<string> callback)
    {
        try
        {
            _importer = new AssetsImporter();
            await _importer.GetReleases("releases", _ct);
            _releasesList.Clear();
            foreach (var release in _importer.Releases)
            {
                _releasesList.Add(release.TagName);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Releases Syncing Exception: {e}");
        }

        callback("リソース読み込みが完了しました");
    }

    private async UniTaskVoid Import(Action<string> callback)
    {
        try
        {
            var id = _importer.Releases[_releasesSelectedIndex].Assets[0].ID;

            var filePath = await _importer.GetAsset("asset", id, _ct);
            Download(filePath);
            callback("アセットのインポートが完了しました");
        }
        catch (Exception e)
        {
            callback("アセットのインポートに失敗しました");
            Debug.LogError($"Importing Exception: {e}");
            throw;
        }
    }

    private void Download(string result)
    {
        try
        {
            if (string.IsNullOrEmpty(result))
            {
                Debug.LogError("ファイルのパスが取得できませんでした");
                return;
            }

            var files = Directory.GetFiles(result, "*.unitypackage", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                AssetDatabase.ImportPackage(file, _showImportWindow);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Download Exception: {e}");
        }

        if (_isFileChecking)
        {
            return;
        }

        Directory.Delete(result, true);
    }

    /// <summary>
    /// GUI.Labelの色を変更して表示するヘルパーメソッド
    /// </summary>
    /// <param name="text">色を変更したいテキスト</param>
    /// <param name="color">変更したい色</param>
    private void DrawColorLabel(string text, Color color)
    {
        var style = GUI.skin.label;
        var styleState = new GUIStyleState
        {
            textColor = color
        };
        style.normal = styleState;

        GUILayout.Label(text, style);

        // Labelの色を元に戻す
        var styleState2 = new GUIStyleState
        {
            textColor = _defaultLabelColor
        };
        style.normal = styleState2;
    }
}