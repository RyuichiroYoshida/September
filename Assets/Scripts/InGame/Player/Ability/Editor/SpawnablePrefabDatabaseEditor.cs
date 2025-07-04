#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SpawnablePrefabDatabase))]
public class SpawnablePrefabDatabaseEditor : Editor
{
    private SerializedProperty _entries;

    private void OnEnable()
    {
        _entries = serializedObject.FindProperty("_entries");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Spawnable Prefabs", EditorStyles.boldLabel);

        int deleteIndex = -1;

        for (int i = 0; i < _entries.arraySize; i++)
        {
            var entry = _entries.GetArrayElementAtIndex(i);
            var guidProp = entry.FindPropertyRelative("Guid");
            var prefabProp = entry.FindPropertyRelative("Prefab");

            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.PropertyField(prefabProp);

            string guid = "";
            if (prefabProp.objectReferenceValue != null)
            {
                var path = AssetDatabase.GetAssetPath(prefabProp.objectReferenceValue);
                guid = AssetDatabase.AssetPathToGUID(path);
                guidProp.stringValue = guid;
            }

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("GUID", guid);
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Copy GUID"))
            {
                EditorGUIUtility.systemCopyBuffer = guid;
                Debug.Log($"Copied GUID: {guid}");
            }

            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("Delete Entry"))
            {
                deleteIndex = i; // 削除はループ後に行う
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        if (deleteIndex >= 0)
        {
            _entries.DeleteArrayElementAtIndex(deleteIndex);
        }

        if (GUILayout.Button("Add Entry"))
        {
            _entries.arraySize++;
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
