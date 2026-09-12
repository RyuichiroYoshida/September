using System;
using System.Reflection;

namespace InGame.Common
{
    public static class CloneUtility
    {
        /// <summary>
        /// オブジェクトのシャローコピー（浅いコピー）を生成します。
        /// リフレクションを用いてメンバワイズクローンを呼び出します。
        /// </summary>
        public static T CloneObject<T>(T source) where T : class
        {
            if (source == null) return null;

            // MemberwiseCloneはprotectedメソッドなので、外側から呼び出すためにはリフレクションを使う
            Type type = source.GetType();
            MethodInfo memberwiseClone = type.GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);
            if (memberwiseClone == null)
            {
                throw new InvalidOperationException($"Failed to find MemberwiseClone method on type {type.FullName}");
            }
            return (T)memberwiseClone.Invoke(source, null);
        }
    }
}
