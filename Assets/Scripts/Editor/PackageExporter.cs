#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Reflection;

public class PackageExporter : EditorWindow
{
    private const string TARGET_PATH = "Assets/AssetStoreTools";
    private const string LAST_TIME_KEY_PREFIX = "FolderDiff_LastExportTime_";

    private Vector2 _scrollPosition;
    private Vector2 _fileScrollPosition;
    private List<FolderStatus> _folderStatuses = new();
    private List<FileStatus> _changedFiles = new();

    private string _query;

    private class FolderStatus
    {
        public string FolderName; // 孫フォルダ名
        public string ParentName; // 子フォルダ名（カテゴリ分け用）
        public string FullPath;
        public bool HasChanged;
        public DateTime LastModified;
        public DateTime LastExportTime;
    }

    private class FileStatus
    {
        public string RelativePath;
        public string FullPath;
        public DateTime LastModified;
        public bool HasUpdatedMeta;
        
        public const string MetaDisplayText = " [+META]";
        public string DisplayText => RelativePath + (HasUpdatedMeta ? MetaDisplayText : "");
    }

    [MenuItem("September/Package Exporter")]
    public static void ShowWindow() => GetWindow<PackageExporter>("Package Exporter");

    private void OnEnable() => RefreshAnalysis();

    void OnGUI()
    {
        DrawTopOperations();
        EditorGUILayout.Space();

        if (GUILayout.Button("差分を再スキャン", GUILayout.Height(30))) RefreshAnalysis();
        EditorGUILayout.Space();

        DrawFolderStatusList();
        EditorGUILayout.Space();

        DrawChangedFilesList();
        EditorGUILayout.Space();

        DrawBottomActions();
    }

    #region UI Drawing

    private void DrawTopOperations()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("各フォルダのエクスポート履歴に基づいて差分を表示しています", EditorStyles.miniLabel);

            var rect = EditorGUILayout.GetControlRect(GUILayout.Width(120));
            if (EditorGUI.DropdownButton(rect, new GUIContent("その他の操作"), FocusType.Passive))
            {
                GenericMenu menu = new GenericMenu();
                menu.AddItem(new GUIContent("すべて更新済みとしてマーク"), false, MarkAllAsUpdated);
                menu.AddItem(new GUIContent("すべての孫フォルダを書き出す"), false, ExportAllFolders);
                menu.DropDown(rect);
            }
        }
    }

    private void DrawFolderStatusList()
    {
        EditorGUILayout.LabelField("パッケージ状態", EditorStyles.boldLabel);
        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(200));
        foreach (var status in _folderStatuses.ToList())
        {
            DrawFolderStatusRow(status);
        }
        EditorGUILayout.EndScrollView();
    }

    private void DrawFolderStatusRow(FolderStatus status)
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.textArea))
        {
            GUI.contentColor = status.HasChanged ? Color.yellow : Color.white;
            string icon = status.HasChanged ? "● [UPDATED] " : "○ ";

            // 「子フォルダ名 > 孫フォルダ名」の形式で表示
            EditorGUILayout.LabelField($"{icon}[{status.ParentName}] {status.FolderName}", GUILayout.Width(250));
            
            string lastExportStr = status.LastExportTime == DateTime.MinValue ? "未" : status.LastExportTime.ToString("yy/MM/dd HH:mm");
            EditorGUILayout.LabelField($"前: {lastExportStr}", GUILayout.Width(120));
            EditorGUILayout.LabelField($"新: {status.LastModified:yy/MM/dd HH:mm}", GUILayout.Width(120));

            if (GUILayout.Button("開く", GUILayout.Width(40)))
            {
                var obj = AssetDatabase.LoadAssetAtPath<DefaultAsset>(status.FullPath);
                EditorGUIUtility.PingObject(obj);
            }

            // 個別エクスポートボタン
            if (GUILayout.Button("Export", GUILayout.Width(50)))
            {
                ExportSingleFolder(status);
                GUIUtility.ExitGUI();
            }

            if (status.HasChanged && GUILayout.Button("変更を検索", GUILayout.Width(100)))
            {
                _query = status.FolderName;
            }
            GUI.contentColor = Color.white;
        }
    }

    private void DrawChangedFilesList()
    {
        EditorGUILayout.LabelField($"更新があったファイル一覧 ({_changedFiles.Count}件)", EditorStyles.boldLabel);

        DrawSearchField();
        
        // パス表示に必要な幅を計算（表示されるファイルのみで計算）
        float maxPathWidth = _changedFiles.Count > 0 ? _changedFiles.Max(f => EditorStyles.label.CalcSize(new GUIContent(f.DisplayText)).x) : 0;
        
        _fileScrollPosition.y = EditorGUILayout.BeginScrollView(new Vector2(0, _fileScrollPosition.y), GUILayout.ExpandHeight(true)).y;
        foreach (var file in GetFiles(_query))
        {
            DrawChangedFileRow(file, maxPathWidth);
        }
        EditorGUILayout.EndScrollView();

        DrawHorizontalScrollbar(maxPathWidth);
    }

    private void DrawSearchField()
    {
        var method = typeof(EditorGUI).GetMethod(
            "ToolbarSearchField",
            BindingFlags.NonPublic | BindingFlags.Static,
            null,
            new[] { typeof(Rect), typeof(string), typeof(bool) },
            null
        );

        if (method != null)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                Rect rect = EditorGUILayout.GetControlRect(GUILayout.ExpandWidth(true));
                _query = (string)method.Invoke(null, new object[] { rect, _query, false });
            }
        }
    }

    private IEnumerable<FileStatus> GetFiles(string query)
    {
        return string.IsNullOrEmpty(query)
            ? _changedFiles
            : _changedFiles.Where(file => file.RelativePath.StartsWith(query));
    }

    private void DrawChangedFileRow(FileStatus file, float maxPathWidth)
    {
        var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(file.FullPath);
        var sourceAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(file.FullPath.Replace(".meta", ""));
        bool isAsset = asset != null;
        bool isMetaFile = sourceAsset != null;
        bool isValidFile = isMetaFile || isAsset;

        using (new EditorGUILayout.HorizontalScope(EditorStyles.textArea))
        {
            // 更新日時
            EditorGUILayout.LabelField(file.LastModified.ToString("yy/MM/dd HH:mm"), GUILayout.Width(100));
            
            if (!isAsset) GUI.contentColor = isMetaFile ? Color.gray : Color.yellow;

            // ファイルパス表示
            Rect pathAreaRect = EditorGUILayout.GetControlRect(GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                GUI.BeginGroup(pathAreaRect);
                Rect labelRect = new Rect(-_fileScrollPosition.x, 0, maxPathWidth + 20, pathAreaRect.height);
                GUI.Label(labelRect, file.DisplayText);
                GUI.EndGroup();
            }

            // ファイルを開く。または削除済み表示
            if (!isValidFile)
            {
                GUI.contentColor = Color.yellow;
                EditorGUILayout.LabelField("削除済み", GUILayout.Width(60));
            }
            else
            {
                GUI.contentColor = Color.white;
                if (GUILayout.Button("開く", GUILayout.Width(40)))
                {
                    EditorGUIUtility.PingObject(isAsset ? asset : sourceAsset);
                }
            }
            GUI.contentColor = Color.white;
        }
    }

    private void DrawHorizontalScrollbar(float maxPathWidth)
    {
        // 下部に水平スクロールバーを表示（パス表示エリアのスクロールを制御）
        // おおよその表示可能幅を計算
        float visiblePathWidth = position.width - 180;
        if (maxPathWidth > visiblePathWidth)
        {
            _fileScrollPosition.x = GUILayout.HorizontalScrollbar(_fileScrollPosition.x, visiblePathWidth, 0, maxPathWidth);
        }
        else
        {
            _fileScrollPosition.x = 0;
        }
    }

    private void DrawBottomActions()
    {
        if (GUILayout.Button("更新があった孫フォルダをすべて書き出す", GUILayout.Height(40)))
        {
            ExportAllChangedFolders();
        }
    }

    #endregion

    #region Analysis Logic

    private void RefreshAnalysis()
    {
        _folderStatuses.Clear();
        _changedFiles.Clear();

        string rootFullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", TARGET_PATH));
        if (!Directory.Exists(rootFullPath)) return;

        List<FileStatus> allTempChanged = new();
        ScanFolders(rootFullPath, allTempChanged);
        ProcessChangedFiles(allTempChanged);

        _folderStatuses = _folderStatuses.OrderByDescending(s => s.HasChanged).ThenBy(s => s.FolderName).ToList();
        _changedFiles = _changedFiles.OrderByDescending(f => f.LastModified).ToList();
    }

    private void ScanFolders(string rootFullPath, List<FileStatus> allTempChanged)
    {
        // 1. 子フォルダを取得
        string[] childDirs = Directory.GetDirectories(rootFullPath);
        foreach (var child in childDirs)
        {
            string childName = Path.GetFileName(child);
            // 2. 孫フォルダを取得
            string[] grandchildDirs = Directory.GetDirectories(child);

            foreach (var grandchild in grandchildDirs)
            {
                AnalyzeGrandchildFolder(child, grandchild, childName, allTempChanged);
            }
        }
    }

    private void AnalyzeGrandchildFolder(string childPath, string grandchildPath, string childName, List<FileStatus> allTempChanged)
    {
        string unityPath = ToUnityPath(grandchildPath);
        DateTime lastExportTime = GetLastExportTime(unityPath);
        var files = Directory.GetFiles(grandchildPath, "*.*", SearchOption.AllDirectories);

        DateTime maxModified = files.Length == 0 ? Directory.GetLastWriteTime(grandchildPath) : DateTime.MinValue;

        foreach (var file in files)
        {
            // 全てのファイルの中から直近の更新日時を見つける
            DateTime modTime = File.GetLastWriteTime(file);
            if (modTime > maxModified) maxModified = modTime;

            // 前回のエクスポートから更新のあったファイルを見つけて一時リストに保存
            if (modTime > lastExportTime)
            {
                allTempChanged.Add(new FileStatus
                {
                    RelativePath = Path.GetRelativePath(childPath, file).Replace("\\", "/"),
                    FullPath = ToUnityPath(file),
                    LastModified = modTime
                });
            }
        }

        _folderStatuses.Add(new FolderStatus
        {
            FolderName = Path.GetFileName(grandchildPath),
            ParentName = childName,
            FullPath = unityPath,
            HasChanged = maxModified > lastExportTime,
            LastModified = maxModified,
            LastExportTime = lastExportTime
        });
    }

    private void ProcessChangedFiles(List<FileStatus> allTempChanged)
    {
        // メタファイルの重複フィルタリングと「+META」フラグの設定
        var changedPaths = new HashSet<string>(allTempChanged.Select(f => f.FullPath));
        foreach (var file in allTempChanged)
        {
            if (file.FullPath != null && file.FullPath.EndsWith(".meta"))
            {
                // 対応するアセットのパスを取得（.metaを除去）
                string assetPath = file.FullPath.Substring(0, file.FullPath.Length - 5);
                // 対応するアセットも更新されている場合は、メタファイル自体は表示しない
                if (changedPaths.Contains(assetPath)) continue;
            }

            // 表示するファイルに対して、対応するメタファイルも更新されているかチェック
            file.HasUpdatedMeta = changedPaths.Contains(file.FullPath + ".meta");
            _changedFiles.Add(file);
        }
    }

    #endregion

    #region Export & Helpers

    private void ExportSingleFolder(FolderStatus status)
    {
        string defaultName = $"{status.FolderName}.unitypackage";
        string exportPath = EditorUtility.SaveFilePanel("Export Grandchild Package", "", defaultName, "unitypackage");

        if (!string.IsNullOrEmpty(exportPath))
        {
            AssetDatabase.ExportPackage(status.FullPath, exportPath, ExportPackageOptions.Recurse);
            Debug.Log($"Exported: {exportPath}");
            SaveCurrentTime(status.FullPath);
            RefreshAnalysis();
        }
    }

    private void MarkAllAsUpdated()
    {
        if (EditorUtility.DisplayDialog("確認", "すべてのフォルダの更新日時を現在時刻に変更します。よろしいですか？", "はい", "いいえ"))
        {
            foreach (var status in _folderStatuses) SaveCurrentTime(status.FullPath);
            RefreshAnalysis();
        }
    }

    private void ExportAllChangedFolders() => ExportFolders(_folderStatuses.Where(s => s.HasChanged).ToList());

    private void ExportAllFolders() => ExportFolders(_folderStatuses);

    private void ExportFolders(List<FolderStatus> targets)
    {
        if (targets.Count == 0) return;

        string folderPath = EditorUtility.OpenFolderPanel("保存先フォルダを選択", "", "");
        if (string.IsNullOrEmpty(folderPath)) return;

        foreach (var target in targets)
        {
            string fileName = Path.Combine(folderPath, $"{target.FolderName}.unitypackage");
            AssetDatabase.ExportPackage(target.FullPath, fileName, ExportPackageOptions.Recurse);
            Debug.Log($"Auto Exported: {fileName}");
            SaveCurrentTime(target.FullPath);
        }
        
        RefreshAnalysis();
        EditorUtility.DisplayDialog("完了", $"{targets.Count}個のパッケージを書き出しました", "OK");
    }

    private DateTime GetLastExportTime(string path)
    {
        string key = GetKey(path);
        string ticks = EditorPrefs.GetString(key, "");
        return long.TryParse(ticks, out long result) ? DateTime.FromBinary(result) : DateTime.MinValue;
    }

    private void SaveCurrentTime(string path) => EditorPrefs.SetString(GetKey(path), DateTime.Now.ToBinary().ToString());
    
    private string GetKey(string path) => LAST_TIME_KEY_PREFIX + path;

    private string ToUnityPath(string fullPath)
    {
        fullPath = fullPath.Replace("\\", "/");
        int index = fullPath.IndexOf("Assets/");
        return index >= 0 ? fullPath.Substring(index) : null;
    }

    #endregion
}
#endif
