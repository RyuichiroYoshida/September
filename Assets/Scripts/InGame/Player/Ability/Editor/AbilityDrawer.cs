#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using InGame.Player.Ability;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(AbilityBase), true)]
public class AbilityDrawer : PropertyDrawer
{
    private const string TYPE_DROPDOWN_LABEL = "Type";

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        if (property.managedReferenceValue == null)
            return EditorGUIUtility.singleLineHeight * 2f;

        return EditorGUI.GetPropertyHeight(property, label, true);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var rect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        DrawTypeSelector(rect, property);

        if (property.managedReferenceValue == null)
            return;

        var ability = property.managedReferenceValue as AbilityBase;
        var labelText = ability?.DisplayName ?? property.managedReferenceValue.GetType().Name;
        var labelOverride = new GUIContent(labelText);

        EditorGUI.BeginProperty(position, labelOverride, property);
        EditorGUI.indentLevel++;

        float y = position.y + EditorGUIUtility.singleLineHeight + 4;

        //個別のプロパティの描画設定
        var abilityNameProp = property.FindPropertyRelative("_abilityName");
        var abilityNameRect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
        EditorGUI.PropertyField(abilityNameRect, abilityNameProp, new GUIContent("アビリティ名"));
        y += EditorGUIUtility.singleLineHeight + 2;
        
        var cooldownProp = property.FindPropertyRelative("_cooldown");
        var cooldownRect = new Rect(position.x, y, position.width, EditorGUIUtility.singleLineHeight);
        EditorGUI.PropertyField(cooldownRect, cooldownProp, new GUIContent("クールダウン"));
        y += EditorGUIUtility.singleLineHeight + 2;

        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }

    private void DrawTypeSelector(Rect rect, SerializedProperty property)
    {
        var baseType = typeof(AbilityBase);
        var types = GetAllSubclasses(baseType);

        var typeNames = types.Select(t => t.Name).ToArray();
        var currentType = property.managedReferenceValue?.GetType();
        int currentIndex = currentType == null ? 0 : Array.FindIndex(types.ToArray(), t => t == currentType);

        int selectedIndex = EditorGUI.Popup(rect, TYPE_DROPDOWN_LABEL, currentIndex, typeNames);

        if (selectedIndex != currentIndex)
        {
            var selectedType = types[selectedIndex];
            property.serializedObject.Update();

            // インスタンス生成と適用
            property.managedReferenceValue = Activator.CreateInstance(selectedType);
            property.serializedObject.ApplyModifiedProperties();
        }
    }

    private static List<Type> GetAllSubclasses(Type baseType)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => !t.IsAbstract && baseType.IsAssignableFrom(t))
            .OrderBy(t => t.Name)
            .ToList();
    }
}
#endif