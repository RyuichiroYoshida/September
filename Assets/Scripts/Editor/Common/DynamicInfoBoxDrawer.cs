#if UNITY_EDITOR
using System;
using September.Common.Attribute;
using UnityEditor;
using UnityEngine;

namespace September.Editor.Common
{
    [CustomPropertyDrawer(typeof(DynamicInfoBoxAttribute))]
    public class DynamicInfoBoxDrawer : PropertyDrawer
    {
        private const float Spacing = 2f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            DynamicInfoBoxAttribute dynamicInfoBox = (DynamicInfoBoxAttribute)attribute;
            object targetObject = GetParentObject(property);
            string message = GetMessage(targetObject, dynamicInfoBox.MethodName);

            if (!string.IsNullOrEmpty(message))
            {
                float propertyHeight = EditorGUI.GetPropertyHeight(property, label, true);
                float infoBoxHeight = EditorStyles.helpBox.CalcHeight(new GUIContent(message), position.width);

                Rect infoBoxRect = new Rect(
                    position.x,
                    position.y,
                    position.width,
                    infoBoxHeight
                );

                EditorGUI.HelpBox(infoBoxRect, message, MessageType.Info);

                Rect propertyRect = new Rect(
                    position.x,
                    position.y + infoBoxHeight + Spacing,
                    position.width,
                    propertyHeight
                );

                EditorGUI.PropertyField(propertyRect, property, label, true);
            }
            else
            {
                EditorGUI.PropertyField(position, property, label, true);
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            DynamicInfoBoxAttribute dynamicInfoBox = (DynamicInfoBoxAttribute)attribute;
            object targetObject = GetParentObject(property);
            string message = GetMessage(targetObject, dynamicInfoBox.MethodName);

            float propertyHeight = EditorGUI.GetPropertyHeight(property, label, true);

            if (!string.IsNullOrEmpty(message))
            {
                float width = Mathf.Max(EditorGUIUtility.currentViewWidth - 40f, 1f);
                float infoBoxHeight = EditorStyles.helpBox.CalcHeight(new GUIContent(message), width);
                return infoBoxHeight + Spacing + propertyHeight;
            }

            return propertyHeight;
        }

        private static string GetMessage(object targetObject, string methodName)
        {
            if (targetObject == null) return null;

            var method = targetObject.GetType().GetMethod(
                methodName,
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance
            );

            if (method == null) return null;
            if (method.ReturnType != typeof(string)) return null;
            if (method.GetParameters().Length != 0) return null;

            return method.Invoke(targetObject, null) as string;
        }

        private static object GetParentObject(SerializedProperty property)
        {
            object obj = property.serializedObject.targetObject;

            string path = property.propertyPath.Replace(".Array.data[", "[");
            string[] elements = path.Split('.');

            for (int i = 0; i < elements.Length - 1; i++)
            {
                obj = GetValue(obj, elements[i]);

                if (obj == null)
                {
                    return null;
                }
            }

            return obj;
        }

        private static object GetValue(object source, string name)
        {
            if (source == null) return null;

            if (name.Contains("["))
            {
                string fieldName = name.Substring(0, name.IndexOf("[", StringComparison.Ordinal));
                string indexText = name.Substring(
                    name.IndexOf("[", StringComparison.Ordinal) + 1,
                    name.IndexOf("]", StringComparison.Ordinal) - name.IndexOf("[", StringComparison.Ordinal) - 1
                );

                object enumerable = GetValue(source, fieldName);

                if (enumerable is System.Collections.IList collection &&
                    int.TryParse(indexText, out int index) &&
                    index < collection.Count)
                {
                    return collection[index];
                }

                return null;
            }

            Type type = source.GetType();

            while (type != null)
            {
                var field = type.GetField(
                    name,
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance
                );

                if (field != null)
                {
                    return field.GetValue(source);
                }

                var property = type.GetProperty(
                    name,
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance
                );

                if (property != null)
                {
                    return property.GetValue(source);
                }

                type = type.BaseType;
            }

            return null;
        }
    }
}
#endif
