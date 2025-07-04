#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Reflection;
using NaughtyAttributes;

[CustomPropertyDrawer(typeof(SerializableDictionary<,>), true)]
public class SerializableDictionaryDrawer : PropertyDrawer
{
    private const float RemoveButtonWidth = 50f;
    private const float AddButtonHeight = 20f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var keysProp = property.FindPropertyRelative("keys");
        return EditorGUIUtility.singleLineHeight * (keysProp.arraySize + 2) + 4f;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var keysProp = property.FindPropertyRelative("keys");
        var valuesProp = property.FindPropertyRelative("values");

        // 🟡 ラベル処理：NaughtyAttributes.LabelAttribute を取得
        string labelText = ObjectNames.NicifyVariableName(property.name);

        if (fieldInfo != null)
        {
            var labelAttr = fieldInfo.GetCustomAttribute<LabelAttribute>();
            if (labelAttr != null)
                labelText = labelAttr.Label;
        }

        EditorGUI.LabelField(
            new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight),
            labelText
        );

        EditorGUI.indentLevel++;

        HashSet<int> usedEnumIndices = new HashSet<int>();

        for (int i = 0; i < keysProp.arraySize; i++)
        {
            var keyProp = keysProp.GetArrayElementAtIndex(i);
            var valueProp = valuesProp.GetArrayElementAtIndex(i);

            float y = position.y + EditorGUIUtility.singleLineHeight * (i + 1);

            float labelWidth = 40f;
            float fieldWidth = (position.width - labelWidth * 2 - RemoveButtonWidth) / 2f;

            Rect keyLabelRect = new Rect(position.x, y, labelWidth, EditorGUIUtility.singleLineHeight);
            Rect keyRect = new Rect(keyLabelRect.xMax, y, fieldWidth, EditorGUIUtility.singleLineHeight);
            Rect valueLabelRect = new Rect(keyRect.xMax, y, labelWidth, EditorGUIUtility.singleLineHeight);
            Rect valueRect = new Rect(valueLabelRect.xMax, y, fieldWidth, EditorGUIUtility.singleLineHeight);
            Rect removeRect = new Rect(position.xMax - RemoveButtonWidth, y, RemoveButtonWidth, EditorGUIUtility.singleLineHeight);

            EditorGUI.LabelField(keyLabelRect, "キー");
            EditorGUI.PropertyField(keyRect, keyProp, GUIContent.none);
            EditorGUI.LabelField(valueLabelRect, "値");
            EditorGUI.PropertyField(valueRect, valueProp, GUIContent.none);

            if (keyProp.propertyType == SerializedPropertyType.Enum)
            {
                usedEnumIndices.Add(keyProp.enumValueIndex);
            }

            if (GUI.Button(removeRect, "削除"))
            {
                keysProp.DeleteArrayElementAtIndex(i);
                valuesProp.DeleteArrayElementAtIndex(i);
                GUI.changed = true;
                break;
            }
        }

        float addY = position.y + EditorGUIUtility.singleLineHeight * (keysProp.arraySize + 1);
        Rect addRect = new Rect(position.x, addY, position.width, AddButtonHeight);

        if (GUI.Button(addRect, "+ 追加"))
        {
            bool added = false;

            if (keysProp.arraySize > 0 && keysProp.GetArrayElementAtIndex(0).propertyType == SerializedPropertyType.Enum)
            {
                var sampleKey = keysProp.GetArrayElementAtIndex(0);
                var enumLength = sampleKey.enumNames.Length;

                for (int i = 0; i < enumLength; i++)
                {
                    if (!usedEnumIndices.Contains(i))
                    {
                        keysProp.arraySize++;
                        valuesProp.arraySize++;

                        var newKey = keysProp.GetArrayElementAtIndex(keysProp.arraySize - 1);
                        newKey.enumValueIndex = i;

                        added = true;
                        break;
                    }
                }

                if (!added)
                {
                    Debug.LogWarning("すべてのEnumが使われています");
                }
            }
            else
            {
                // Non-enum keys (fallback)
                keysProp.arraySize++;
                valuesProp.arraySize++;
            }
        }

        EditorGUI.indentLevel--;
    }
}
#endif
