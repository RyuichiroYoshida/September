using UnityEditor;
using UnityEngine;
using UnityEngine.Splines.Interpolators;

namespace September.Common
{
    [System.Serializable]
    public struct MinMaxRange
    {
        public MinMaxRange(float min, float max)
        {
            Min  = min;
            Max = max;
        }

        public float Min;
        public float Max;

        /// <summary>
        /// 閉区間内であるか調べます
        /// </summary>
        public bool IsInRange(float value)
        {
            return value >= Min && value <= Max;
        }

        public static MinMaxRange Lerp(MinMaxRange from, MinMaxRange to, float t)
        {
            return new MinMaxRange(Mathf.Lerp(from.Min, to.Min, t), Mathf.Lerp(from.Max, to.Max, t));
        }

#if UNITY_EDITOR
        [CustomPropertyDrawer(typeof(MinMaxRange))]
        public class MinMaxRangeDrawer : PropertyDrawer
        {
            public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
            {
                var minProp = property.FindPropertyRelative("Min");
                var maxProp = property.FindPropertyRelative("Max");

                EditorGUI.BeginProperty(position, label, property);

                position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

                float[] values = { minProp.floatValue, maxProp.floatValue };
                EditorGUI.MultiFloatField(position, new[]{ new GUIContent("Min"), new GUIContent("Max")}, values);
                minProp.floatValue = values[0];
                maxProp.floatValue = values[1];

                EditorGUI.EndProperty();
            }
        }
#endif
    }
}
