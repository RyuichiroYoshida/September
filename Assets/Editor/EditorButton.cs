using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using UnityEngine;
using UnityEditor;

public class EditorButton : EditorWindow
{

    private static Dictionary<string, string> _packages = new Dictionary<string, string>
    {
        { "Package1", "https://drive.google.com/uc?export=download&id=13EUNNvpz1tmuvZ1CINxtHA5QCwl3yj7U" }
    };
    
    private static string _saveDirectory = "Assets/DownloadedPackages/";
    [MenuItem("September/Import")]
    public static void ShowWindow()
    {
        var window = EditorWindow.GetWindow<EditorButton>("ImportWindow");
        window.Show();
    }

    private void OnGUI()
    {
        if (GUILayout.Button("インポート"))
        {
           DownLoad();
        }
    }

    private void DownLoad()
    {
        if (!Directory.Exists(_saveDirectory))
        {
            Directory.CreateDirectory(_saveDirectory);
            Debug.Log($"Created directory: {_saveDirectory}");
        }

        if (_packages.Count == 0)
        {
            Debug.LogWarning("URLが一つも存在しません");
        }

        foreach (var package in _packages)
        {
            string packageName = package.Key;
            string url = package.Value;
            string savePath =Path.Combine(_saveDirectory, packageName + ".unitypackage");
            
            if (File.Exists(savePath))
            {
                Debug.Log($"Skipping download: {packageName} (Already exists)");
            }
            else
            {
                using (WebClient client = new WebClient())
                {
                    try
                    {
                        client.DownloadFile(url, savePath);
                        Debug.Log($"Downloaded: {packageName} -> {savePath}");
                    }
                    catch(Exception e)
                    {
                        Debug.LogError($"Failed to download {packageName}: {e.Message}");
                    }
                }
            }
             
            AssetDatabase.ImportPackage(savePath, false);
          
        }
        Debug.Log("All packages processed.");
    }
    
    
}
