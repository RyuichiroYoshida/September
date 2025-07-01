#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

/// <summary>
/// SubclassSelectorDrawer - SerializeReference + List<T> 対応
/// </summary>
[CustomPropertyDrawer(typeof(SubclassSelectorAttribute))]
public class SubclassSelectorDrawer : PropertyDrawer
{
    private bool _initialized;
    private Type[] _inheritedTypes;
    private string[] _typePopupNames;
    private string[] _typeFullNames;
    private int _currentTypeIndex;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.ManagedReference)
        {
            EditorGUI.LabelField(position, label.text, "Use [SerializeReference]");
            return;
        }

        if (!_initialized)
        {
            Initialize(property);
            _initialized = true;
        }

        // 要素が null の場合、デフォルトの型（0番）で初期化
        if (property.managedReferenceValue == null)
        {
            var defaultType = _inheritedTypes[0];
            if (defaultType != null)
            {
                property.managedReferenceValue = Activator.CreateInstance(defaultType);
                property.serializedObject.ApplyModifiedProperties();
            }
        }

        // 現在の型インデックス取得
        GetCurrentTypeIndex(property.managedReferenceFullTypename);

        // --- 型選択ポップアップ ---
        Rect popupRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        int selectedTypeIndex = EditorGUI.Popup(popupRect, _currentTypeIndex, _typePopupNames);

        if (_currentTypeIndex != selectedTypeIndex)
        {
            _currentTypeIndex = selectedTypeIndex;
            Type selectedType = _inheritedTypes[selectedTypeIndex];
            property.managedReferenceValue = Activator.CreateInstance(selectedType);
            property.serializedObject.ApplyModifiedProperties();
        }

        // --- 中身描画 ---
        Rect fieldRect = new Rect(
            position.x,
            position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing,
            position.width,
            EditorGUI.GetPropertyHeight(property, GUIContent.none, true)
        );

        EditorGUI.PropertyField(fieldRect, property, GUIContent.none, true);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float fieldHeight = EditorGUI.GetPropertyHeight(property, GUIContent.none, true);
        return EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing + fieldHeight;
    }

    private void Initialize(SerializedProperty property)
    {
        var baseType = GetManagedReferenceFieldType(property);
        var includeMono = ((SubclassSelectorAttribute)attribute).IsIncludeMono();
        _inheritedTypes = GetSubclasses(baseType, includeMono).Prepend(null).ToArray();
        Debug.Log(_inheritedTypes.Length + " types found for " + baseType.Name);
        _typePopupNames = _inheritedTypes.Select(t => t == null ? "<null>" : t.Name).ToArray();
        _typeFullNames = _inheritedTypes.Select(t => t == null ? "" : $"{t.Assembly.GetName().Name} {t.FullName}").ToArray();
    }

    private void GetCurrentTypeIndex(string fullTypeName)
    {
        _currentTypeIndex = Array.IndexOf(_typeFullNames, fullTypeName);
        if (_currentTypeIndex < 0) _currentTypeIndex = 0;
    }

    /// <summary>
    /// property から ManagedReference のベース型を取得する（List対応）
    /// </summary>
    private static Type GetManagedReferenceFieldType(SerializedProperty property)
    {
        string fullTypeName = property.managedReferenceFieldTypename;
        if (string.IsNullOrEmpty(fullTypeName)) return null;

        // Unityの内部表現: "AssemblyName FullName"
        var parts = fullTypeName.Split(' ');
        if (parts.Length != 2) return null;

        string assemblyName = parts[0];
        string classFullName = parts[1];

        var assembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == assemblyName);
        if (assembly == null) return null;

        return assembly.GetType(classFullName);
    }

    private static IEnumerable<Type> GetSubclasses(Type baseType, bool includeMono)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => SafeGetTypes(a))
            .Where(t =>
                t.IsClass &&
                !t.IsAbstract &&
                baseType.IsAssignableFrom(t) &&
                (includeMono || !typeof(MonoBehaviour).IsAssignableFrom(t))
            );
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch { return Enumerable.Empty<Type>(); }
    }
}

#endif
