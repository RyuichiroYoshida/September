using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class EditorButton : EditorWindow
{
    private bool _showImportWindow;

    private bool _isInitialized = true;
    private Color _defaultLabelColor;
    
    private AssetsImporter _importer = new();
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
        
        DrawColorLabel("インポートが終わるまでUnityのシーンを再生しないでください！", Color.red);

        // UnityPackageをインポートするときのファイル選択ウィンドウを表示するかのフラグ (デフォルトは表示しない false)
        _showImportWindow = EditorGUILayout.Toggle("アセットの手動インポート", _showImportWindow);

        // 状態を表示
        EditorGUILayout.LabelField(_showImportWindow
            ? "現在、アセットインポート時にインポートするフォルダを選べます"
            : "現在、自動的に全てのアセットの中身がインポートされます");

        if (GUILayout.Button("インポート"))
        {
            _ = DownLoad();
        }
        
        if (GUILayout.Button("test"))
        {
        }
    }

    private void OnDisable()
    {
        _importer.Dispose();
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