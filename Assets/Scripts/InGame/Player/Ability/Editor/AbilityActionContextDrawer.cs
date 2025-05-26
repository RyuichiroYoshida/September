#if UNITY_EDITOR
using System;
using System.Linq;
using System.Collections.Generic;
using InGame.Player.Ability;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(AbilityActionContext))]
public class AbilityActionContextDrawer : PropertyDrawer
{
    private static List<Type> _conditionTypes;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float totalHeight = EditorGUIUtility.singleLineHeight + 4f; // タイプ選択ドロップダウン分
        return totalHeight;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var conditionProp = property.FindPropertyRelative("Condition");
        var abilityProp = property.FindPropertyRelative("AbilityName");
        var actionProp = property.FindPropertyRelative("ActionType");

        EditorGUI.BeginProperty(position, label, property);

        float y = position.y;
        float h = EditorGUIUtility.singleLineHeight;
        float space = 4f;

        // ✅ 条件未設定のときだけ選択ボタンを表示
        if (conditionProp.managedReferenceValue == null)
        {
            var typeSelectRect = new Rect(position.x, y, position.width, h);
            if (GUI.Button(typeSelectRect, "条件を選択..."))
            {
                ShowTypeDropdown(conditionProp, typeof(IActionCondition));
            }

            EditorGUI.EndProperty();
            return; // 条件未設定時はこれ以上描画しない
        }

        y += 0f; // 高さ据え置き（選択ボタン非表示）

        if (conditionProp.managedReferenceValue is ButtonActionConditionBase)
        {
            var buttonProp = conditionProp.FindPropertyRelative("Button");
            var triggerProp = conditionProp.FindPropertyRelative("TriggerType");

            float buttonWidth = GetEnumDisplayWidth(buttonProp);
            float triggerWidth = GetEnumDisplayWidth(triggerProp);
            float abilityWidth = GetEnumDisplayWidth(abilityProp);
            float actionWidth = GetEnumDisplayWidth(actionProp);

            var rect1 = new Rect(position.x, y, buttonWidth, h);
            var label1 = new Rect(rect1.xMax + space, y, 20f, h);
            var rect2 = new Rect(label1.xMax + space, y, triggerWidth, h);
            var label2 = new Rect(rect2.xMax + space, y, 50f, h);
            var rect3 = new Rect(label2.xMax + space, y, abilityWidth, h);
            var label3 = new Rect(rect3.xMax + space, y, 20f, h);
            var rect4 = new Rect(label3.xMax + space, y, actionWidth, h);
            var label4 = new Rect(rect4.xMax + space, y, 20f, h);

            EditorGUI.PropertyField(rect1, buttonProp, GUIContent.none);
            EditorGUI.LabelField(label1, "を");
            EditorGUI.PropertyField(rect2, triggerProp, GUIContent.none);
            EditorGUI.LabelField(label2, "したとき");
            EditorGUI.PropertyField(rect3, abilityProp, GUIContent.none);
            EditorGUI.LabelField(label3, "を");
            EditorGUI.PropertyField(rect4, actionProp, GUIContent.none);
            EditorGUI.LabelField(label4, "する");
        }
        else
        {
            string conditionText = conditionProp.managedReferenceValue is IActionCondition cond
                ? cond.DisplayConditionName
                : "[条件未設定]";

            float conditionLabelWidth = EditorStyles.label.CalcSize(new GUIContent(conditionText)).x + 10f;
            var condLabel = new Rect(position.x, y, conditionLabelWidth, h);
            var tokiLabel = new Rect(condLabel.xMax + space, y, 25f, h);

            float abilityWidth = GetEnumDisplayWidth(abilityProp);
            float actionWidth = GetEnumDisplayWidth(actionProp);

            var abilityRect = new Rect(tokiLabel.xMax + space, y, abilityWidth, h);
            var labelWo = new Rect(abilityRect.xMax + space, y, 20f, h);
            var actionRect = new Rect(labelWo.xMax + space, y, actionWidth, h);
            var labelSuru = new Rect(actionRect.xMax + space, y, 20f, h);

            EditorGUI.LabelField(condLabel, conditionText);
            EditorGUI.LabelField(tokiLabel, "とき");
            EditorGUI.PropertyField(abilityRect, abilityProp, GUIContent.none);
            EditorGUI.LabelField(labelWo, "を");
            EditorGUI.PropertyField(actionRect, actionProp, GUIContent.none);
            EditorGUI.LabelField(labelSuru, "する");
        }

        EditorGUI.EndProperty();
    }


    private float GetEnumDisplayWidth(SerializedProperty prop)
    {
        if (prop == null || prop.propertyType != SerializedPropertyType.Enum)
            return 80f;

        var currentLabel = prop.enumDisplayNames[prop.enumValueIndex];
        return EditorStyles.popup.CalcSize(new GUIContent(currentLabel)).x;
    }


    private static void ShowTypeDropdown(SerializedProperty prop, Type baseType)
    {
        if (_conditionTypes == null)
        {
            _conditionTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .Where(t => !t.IsAbstract && baseType.IsAssignableFrom(t))
                .OrderBy(t => t.Name)
                .ToList();
        }

        var menu = new GenericMenu();

        foreach (var type in _conditionTypes)
        {
            string displayName;

            try
            {
                // 🔽 一時的にインスタンスを作成して表示名を取得
                var instance = Activator.CreateInstance(type) as IActionCondition;
                displayName = instance?.DisplayConditionSelectName ?? type.Name;
            }
            catch
            {
                // 万一コンストラクタで失敗したら型名を表示
                displayName = type.Name;
            }

            menu.AddItem(new GUIContent(displayName), false, () =>
            {
                prop.serializedObject.Update();
                var newInstance = Activator.CreateInstance(type);
                prop.managedReferenceValue = newInstance;
                prop.serializedObject.ApplyModifiedProperties();
            });
        }

        menu.ShowAsContext();
    }

}
#endif