#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates an editable animation clip from the current local pose of a selected hierarchy.
/// </summary>
public static class PoseMotionClipCreator
{
    private const float DefaultFrameRate = 60f;
    private const float DefaultDuration = 1f;

    [MenuItem("Tools/Animation/Create Motion Clip From Selected Pose")]
    private static void CreateFromSelectedPose()
    {
        var root = Selection.activeGameObject;
        if (root == null)
        {
            EditorUtility.DisplayDialog("Create Motion Clip", "Select a character root GameObject in the Hierarchy.", "OK");
            return;
        }

        var path = EditorUtility.SaveFilePanelInProject(
            "Create Motion Clip From Pose",
            $"{root.name}_Pose.anim",
            "anim",
            "Save an editable one-second .anim clip from the selected pose.",
            GetDefaultDirectory(root));

        if (string.IsNullOrEmpty(path)) return;

        var clip = new AnimationClip
        {
            name = Path.GetFileNameWithoutExtension(path),
            frameRate = DefaultFrameRate
        };

        AddPoseCurves(clip, root.transform);
        AssetDatabase.CreateAsset(clip, path);
        AssetDatabase.SaveAssets();
        Selection.activeObject = clip;
        EditorGUIUtility.PingObject(clip);
        Debug.Log($"[PoseMotionClipCreator] Created pose clip: {path}", clip);
    }

    [MenuItem("Tools/Animation/Create Motion Clip From Selected Pose", true)]
    private static bool CanCreateFromSelectedPose()
    {
        return Selection.activeGameObject != null;
    }

    private static string GetDefaultDirectory(GameObject root)
    {
        var prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root);
        if (string.IsNullOrEmpty(prefabPath)) return "Assets";

        var directory = Path.GetDirectoryName(prefabPath);
        return string.IsNullOrEmpty(directory) ? "Assets" : directory.Replace('\\', '/');
    }

    private static void AddPoseCurves(AnimationClip clip, Transform root)
    {
        foreach (var transform in root.GetComponentsInChildren<Transform>(true))
        {
            var path = GetRelativePath(root, transform);
            AddVector3Curves(clip, path, "localPosition", transform.localPosition);
            AddQuaternionCurves(clip, path, transform.localRotation);
            AddVector3Curves(clip, path, "localScale", transform.localScale);
        }
    }

    private static void AddVector3Curves(AnimationClip clip, string path, string propertyName, Vector3 value)
    {
        AddCurve(clip, path, $"{propertyName}.x", value.x);
        AddCurve(clip, path, $"{propertyName}.y", value.y);
        AddCurve(clip, path, $"{propertyName}.z", value.z);
    }

    private static void AddQuaternionCurves(AnimationClip clip, string path, Quaternion value)
    {
        AddCurve(clip, path, "localRotation.x", value.x);
        AddCurve(clip, path, "localRotation.y", value.y);
        AddCurve(clip, path, "localRotation.z", value.z);
        AddCurve(clip, path, "localRotation.w", value.w);
    }

    private static void AddCurve(AnimationClip clip, string path, string propertyName, float value)
    {
        var binding = EditorCurveBinding.FloatCurve(path, typeof(Transform), propertyName);
        AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Constant(0f, DefaultDuration, value));
    }

    private static string GetRelativePath(Transform root, Transform target)
    {
        if (root == target) return string.Empty;

        var names = new Stack<string>();
        var current = target;
        while (current != root)
        {
            names.Push(current.name);
            current = current.parent;
        }

        return string.Join("/", names);
    }
}
#endif
