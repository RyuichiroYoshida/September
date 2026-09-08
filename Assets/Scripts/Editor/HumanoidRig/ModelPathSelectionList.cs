#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace September.Editor.HumanoidRig
{
    /// <summary>一覧 1 行に出す状態表示 (色付きラベルと詳細)。</summary>
    internal readonly struct ModelRowInfo
    {
        public string Status { get; }
        public Color StatusColor { get; }
        public string Detail { get; }

        public ModelRowInfo(string status, Color statusColor, string detail)
        {
            Status = status;
            StatusColor = statusColor;
            Detail = detail;
        }

        public static ModelRowInfo Plain(string detail) => new ModelRowInfo(string.Empty, Color.gray, detail);
    }

    /// <summary>
    /// モデルアセットのパス一覧をチェックボックス付きで描画し、選択状態を保持する。
    /// 行の状態表示は呼び出し側のデリゲートに委ねるため、どのタブからも使い回せる。
    /// </summary>
    internal sealed class ModelPathSelectionList
    {
        private const float PathColumnWidth = 320f;
        private const float StatusColumnWidth = 64f;

        private readonly List<string> _paths = new List<string>();
        private readonly HashSet<string> _selected = new HashSet<string>(StringComparer.Ordinal);
        private Vector2 _scroll;
        private GUIStyle _statusStyle;

        public int Count => _paths.Count;

        public IReadOnlyList<string> Selected => _paths.Where(_selected.Contains).ToList();

        public int SelectedCount => _paths.Count(_selected.Contains);

        public void SetPaths(IEnumerable<string> paths)
        {
            _paths.Clear();
            _paths.AddRange(paths);
            _selected.IntersectWith(_paths);
        }

        public void Clear()
        {
            _paths.Clear();
            _selected.Clear();
        }

        public void Draw(string detailHeader, Func<string, ModelRowInfo> rowInfo, string emptyMessage)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("全選択", EditorStyles.toolbarButton, GUILayout.Width(60f)))
                {
                    foreach (var path in _paths) _selected.Add(path);
                }
                if (GUILayout.Button("全解除", EditorStyles.toolbarButton, GUILayout.Width(60f)))
                {
                    _selected.Clear();
                }
                GUILayout.Label("パス", GUILayout.Width(PathColumnWidth));
                GUILayout.Label("状態", GUILayout.Width(StatusColumnWidth));
                GUILayout.Label(detailHeader);
            }

            using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll))
            {
                _scroll = scroll.scrollPosition;
                if (_paths.Count == 0)
                {
                    EditorGUILayout.HelpBox(emptyMessage, MessageType.Info);
                    return;
                }
                foreach (var path in _paths) DrawRow(path, rowInfo(path));
            }
        }

        private void DrawRow(string path, ModelRowInfo info)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                bool selected = _selected.Contains(path);
                bool now = EditorGUILayout.Toggle(selected, GUILayout.Width(18f));
                if (now != selected)
                {
                    if (now) _selected.Add(path);
                    else _selected.Remove(path);
                }

                if (GUILayout.Button(path, EditorStyles.linkLabel, GUILayout.Width(PathColumnWidth)))
                {
                    EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(path));
                }

                // GUIStyle は毎行・毎再描画で作ると無駄に GC を踏むため使い回す。
                _statusStyle ??= new GUIStyle(EditorStyles.label);
                _statusStyle.normal.textColor = info.StatusColor;
                GUILayout.Label(info.Status, _statusStyle, GUILayout.Width(StatusColumnWidth));

                EditorGUILayout.LabelField(info.Detail, EditorStyles.wordWrappedLabel);
            }
        }
    }
}
#endif
