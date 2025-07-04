#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using InGame.Player.Ability;
using September.Common;

[CustomEditor(typeof(AbilityInputHandler))]
public class AbilityInputHandlerEditor : Editor
{
    private ReorderableList _contextList;

    private void OnEnable()
    {
        var prop = serializedObject.FindProperty("_abilityActionContexts");

        _contextList = new ReorderableList(serializedObject, prop, true, true, true, true);

        _contextList.drawHeaderCallback = rect =>
        {
            EditorGUI.LabelField(rect, "アビリティ発動条件一覧");
        };

        _contextList.drawElementCallback = (rect, index, isActive, isFocused) =>
        {
            var element = prop.GetArrayElementAtIndex(index);
            EditorGUI.PropertyField(rect, element, GUIContent.none, true);
        };

        _contextList.elementHeightCallback = index =>
        {
            var element = prop.GetArrayElementAtIndex(index);
            return EditorGUI.GetPropertyHeight(element, true) + 4f;
        };

        _contextList.onAddCallback = list =>
        {
            prop.arraySize++;
            var newElement = prop.GetArrayElementAtIndex(prop.arraySize - 1);

            // AbilityName/ActionType はデフォルトでOK
            newElement.FindPropertyRelative("AbilityName").enumValueIndex = 0;
            newElement.FindPropertyRelative("ActionType").enumValueIndex = 0;

            // Condition は null のまま = ユーザーが選ぶ
            newElement.FindPropertyRelative("Condition").managedReferenceValue = null;

            serializedObject.ApplyModifiedProperties();
        };

    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        _contextList.DoLayoutList();
        serializedObject.ApplyModifiedProperties();
    }
}
#endif
