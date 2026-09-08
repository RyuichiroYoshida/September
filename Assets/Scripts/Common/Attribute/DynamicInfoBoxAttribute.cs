using System;
using UnityEngine;

namespace September.Common.Attribute
{
    [AttributeUsage(AttributeTargets.Field)]
    public class DynamicInfoBoxAttribute : PropertyAttribute
    {
        public string MethodName { get; }

        public DynamicInfoBoxAttribute(string methodName)
        {
            MethodName = methodName;
        }
    }
}
