using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class EditorButton : EditorWindow
{
    private readonly CancellationTokenSource _cts = new();
    private CancellationToken _ct;

    private ReactiveProperty<bool> _syncReleases = new();
    private string _importText;
    
    private int _releasesSelectedIndex;
    private List<string> _releasesList = new();
    
    private bool _showImportWindow;
    private bool _isInitialized = true;
    private Color _defaultLabelColor;

    private AssetsImporter _importer;
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
        
        if (GUILayout.Button("インポート"))
        {
            _ = DownLoad();
        }

        GUILayout.Space(10);

        if (GUILayout.Button("Sync Releases"))
        {
            _syncReleases.Value = false;
            _ = Sync();
        }

        GUILayout.Label(_importText);

        GUILayout.Space(10);

        if (GUILayout.Button("Assets"))
        {
            _ = Import();
        }
    }

    private void OnEnable()
    {
        _ct = _cts.Token;

        _syncReleases.Subscribe(completed => _importText = completed ? "リソース読み込みが完了しました" : "リソース読み込み待機中");
        
        _ = Sync();
    }

    private void OnDisable()
    {
        _cts.Cancel();
        _importer.Dispose();
    }

    private async UniTaskVoid Sync()
    {
        _importer = new AssetsImporter();
        await _importer.GetReleases("releases", _ct);
        _releasesList.Clear();
        foreach (var release in _importer.Releases)
        {
            _releasesList.Add(release.TagName);
        }
        _syncReleases.Value = true;
    }

    private async UniTaskVoid Import()
    {
        var id = _importer.Releases[_releasesSelectedIndex].Assets[0].ID;
        
        await _importer.GetAssetUrl("asset", id, _ct);
    }

    private async Task DownLoad()
    {
        // UnityPackageの存在チェックはAssetsImporter.csで行うので、ここではやらない

        var task = _importer.StartFetching();
        var result = await task;

        if (string.IsNullOrEmpty(result))
        {
            Debug.LogError("ファイルのパスが取得できませんでした");
            return;
        }

        try
        {
            var files = Directory.GetFiles(result, "*.unitypackage", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                AssetDatabase.ImportPackage(file, _showImportWindow);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Exception {e}");
        }
        finally
        {
            Directory.Delete(result, true);
        }
    }

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