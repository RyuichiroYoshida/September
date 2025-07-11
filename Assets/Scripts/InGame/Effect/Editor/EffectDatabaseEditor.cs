#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EffectDatabase))]
public class EffectDatabaseEditor : Editor
{
    private SerializedProperty _effects;

    private void OnEnable()
    {
        _effects = serializedObject.FindProperty("_effects");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Effect Prefabs", EditorStyles.boldLabel);

        int deleteIndex = -1;

        for (int i = 0; i < _effects.arraySize; i++)
        {
            var entry = _effects.GetArrayElementAtIndex(i);
            var effectType = entry.FindPropertyRelative("EffectType");
            var guidProp = entry.FindPropertyRelative("Guid");
            var prefabProp = entry.FindPropertyRelative("Prefab");

            EditorGUILayout.BeginVertical("box");
            effectType.enumValueIndex = EditorGUILayout.Popup(effectType.enumValueIndex, effectType.enumDisplayNames);
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

            GUI.backgroundColor = Color.red;
            if (GUILayout.Button("Delete Effect"))
            {
                deleteIndex = i; // 削除はループ後に行う
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        if (deleteIndex >= 0)
        {
            _effects.DeleteArrayElementAtIndex(deleteIndex);
        }

        if (GUILayout.Button("Add Effect"))
        {
            _effects.arraySize++;
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
