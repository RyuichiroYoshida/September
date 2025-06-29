#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;
using InGame.Interact;

[CustomEditor(typeof(InteractableBase), true)]
public class InteractableBaseEditor : Editor
{
    private ReorderableList _effectList;
    private SerializedProperty _effectListProperty;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var prop = serializedObject.GetIterator();
        bool enterChildren = true;

        while (prop.NextVisible(enterChildren))
        {
            enterChildren = false;

            if (prop.name == "_characterEffects")
            {
                EditorGUILayout.Space(10);
                _effectList.DoLayoutList();
            }
            else
            {
                EditorGUILayout.PropertyField(prop, true);
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void OnEnable()
    {
        _effectListProperty = serializedObject.FindProperty("_characterEffects");

        _effectList = new ReorderableList(serializedObject, _effectListProperty, true, true, true, true);

        _effectList.drawHeaderCallback = rect =>
        {
            EditorGUI.LabelField(rect, "インタラクト時キャラ別効果");
        };

        _effectList.drawElementCallback = (rect, index, isActive, isFocused) =>
        {
            var element = _effectListProperty.GetArrayElementAtIndex(index);
            rect.y += 2;

            // ドロップダウン（型選択）
            var type = element.managedReferenceValue?.GetType();
            var typeName = type != null ? type.Name : "<null>";
            var dropdownRect = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);

            if (EditorGUI.DropdownButton(dropdownRect, new GUIContent(typeName), FocusType.Keyboard))
            {
                ShowTypeSelectorMenu(element);
            }

            // プロパティの表示
            if (element.managedReferenceValue != null)
            {
                EditorGUI.indentLevel++;
                var contentRect = new Rect(rect.x, rect.y + EditorGUIUtility.singleLineHeight + 2, rect.width, rect.height);
                EditorGUI.PropertyField(contentRect, element, GUIContent.none, true);
                EditorGUI.indentLevel--;
            }
        };

        _effectList.elementHeightCallback = index =>
        {
            var element = _effectListProperty.GetArrayElementAtIndex(index);
            float height = EditorGUIUtility.singleLineHeight + 4;

            if (element.managedReferenceValue != null)
            {
                height += EditorGUI.GetPropertyHeight(element, true) + 4;
            }

            return height;
        };

        _effectList.onAddCallback = list =>
        {
            ShowTypeSelectorMenu(null);
        };
    }

    private void ShowTypeSelectorMenu(SerializedProperty targetProperty)
    {
        var menu = new GenericMenu();
        var baseType = typeof(CharacterInteractEffectBase);

        var types = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(asm => asm.GetTypes())
            .Where(t => !t.IsAbstract && baseType.IsAssignableFrom(t))
            .ToList();

        foreach (var t in types)
        {
            menu.AddItem(new GUIContent(t.Name), false, () =>
            {
                var instance = Activator.CreateInstance(t);
                if (targetProperty == null)
                {
                    int index = _effectListProperty.arraySize;
                    _effectListProperty.InsertArrayElementAtIndex(index);
                    targetProperty = _effectListProperty.GetArrayElementAtIndex(index);
                }

                targetProperty.managedReferenceValue = instance;
                serializedObject.ApplyModifiedProperties();
            });
        }

        menu.ShowAsContext();
    }
}
#endif
