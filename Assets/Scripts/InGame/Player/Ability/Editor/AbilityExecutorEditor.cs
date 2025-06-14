#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using InGame.Player.Ability;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

[CustomEditor(typeof(AbilityExecutor))]
public class AbilityExecutorEditor : Editor
{
    private ReorderableList _list;
    private List<bool> _foldouts = new();
    private SerializedProperty _abilityReferencesProp;

    private void OnEnable()
    {
        _abilityReferencesProp = serializedObject.FindProperty("_abilityReferences");

        _list = new ReorderableList(serializedObject, _abilityReferencesProp, true, true, true, true);

        _list.drawHeaderCallback = rect => { EditorGUI.LabelField(rect, "アビリティ一覧"); };

        _list.onAddCallback = list =>
        {
            GenericMenu menu = new GenericMenu();
            foreach (var type in GetAllDerivedTypes<AbilityBase>())
            {
                menu.AddItem(new GUIContent(type.Name), false, () =>
                {
                    int index = _abilityReferencesProp.arraySize;
                    _abilityReferencesProp.InsertArrayElementAtIndex(index);
                    var element = _abilityReferencesProp.GetArrayElementAtIndex(index);
                    element.managedReferenceValue = System.Activator.CreateInstance(type);
                    serializedObject.ApplyModifiedProperties();
                });
            }

            menu.ShowAsContext();
        };

        _list.drawElementCallback = (rect, index, isActive, isFocused) =>
        {
            if (index >= _foldouts.Count)
                _foldouts.Add(false);

            var element = _abilityReferencesProp.GetArrayElementAtIndex(index);
            var instance = element.managedReferenceValue as AbilityBase;
            string label = instance?.DisplayName ?? $"Element {index}";

            float handleWidth = 20f;
            var foldoutRect = new Rect(rect.x + handleWidth, rect.y, rect.width - handleWidth,
                EditorGUIUtility.singleLineHeight);
            _foldouts[index] = EditorGUI.Foldout(foldoutRect, _foldouts[index], label, true);

            if (_foldouts[index] && instance != null)
            {
                float y = foldoutRect.y + EditorGUIUtility.singleLineHeight + 2;
                float spacing = 2f;

                var instanceType = instance.GetType();
                var baseType = typeof(AbilityBase);
                var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

                var baseFieldNames = baseType
                    .GetFields(flags)
                    .Select(f => f.Name)
                    .ToHashSet();

                var derivedFieldNames = instanceType
                    .GetFields(flags)
                    .Where(f => f.DeclaringType == instanceType)
                    .Select(f => f.Name)
                    .ToHashSet();

                var iterator = element.Copy();
                var end = iterator.GetEndProperty();
                iterator.NextVisible(true);

                while (!SerializedProperty.EqualContents(iterator, end))
                {
                    bool isBase = baseFieldNames.Contains(iterator.name);
                    bool isDerived = derivedFieldNames.Contains(iterator.name);

                    if (!isBase && !isDerived)
                    {
                        iterator.NextVisible(false);
                        continue;
                    }

                    string displayLabel = iterator.name switch
                    {
                        "_abilityName" => "アビリティ名",
                        "_cooldown" => "クールダウン",
                        _ => ObjectNames.NicifyVariableName(iterator.name)
                    };

                    float height = EditorGUI.GetPropertyHeight(iterator, true);
                    var fieldRect = new Rect(rect.x, y, rect.width, height);
                    EditorGUI.PropertyField(fieldRect, iterator, new GUIContent(displayLabel), true);
                    y += height + spacing;

                    iterator.NextVisible(false);
                }
            }
        };


        _list.elementHeightCallback = index =>
        {
            if (index >= _foldouts.Count)
                return EditorGUIUtility.singleLineHeight + 4;

            var element = _abilityReferencesProp.GetArrayElementAtIndex(index);
            var instance = element.managedReferenceValue as AbilityBase;
            if (!_foldouts[index] || instance == null)
                return EditorGUIUtility.singleLineHeight + 4;

            var instanceType = instance.GetType();
            var baseType = typeof(AbilityBase);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

            var baseFieldNames = baseType.GetFields(flags).Select(f => f.Name).ToHashSet();
            var derivedFieldNames = instanceType.GetFields(flags)
                .Where(f => f.DeclaringType == instanceType)
                .Select(f => f.Name)
                .ToHashSet();

            float total = EditorGUIUtility.singleLineHeight + 2;
            var iterator = element.Copy();
            var end = iterator.GetEndProperty();
            iterator.NextVisible(true);

            while (!SerializedProperty.EqualContents(iterator, end))
            {
                bool isBase = baseFieldNames.Contains(iterator.name);
                bool isDerived = derivedFieldNames.Contains(iterator.name);

                if (!isBase && !isDerived)
                {
                    iterator.NextVisible(false);
                    continue;
                }

                total += EditorGUI.GetPropertyHeight(iterator, true) + 2;
                iterator.NextVisible(false);
            }

            return total + 2;
        };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        _list.DoLayoutList();
        serializedObject.ApplyModifiedProperties();
    }

    public static List<System.Type> GetAllDerivedTypes<T>()
    {
        return System.AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => !t.IsAbstract && typeof(T).IsAssignableFrom(t))
            .ToList();
    }
}
#endif