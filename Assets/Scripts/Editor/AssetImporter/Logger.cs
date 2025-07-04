using UnityEngine;

namespace September.Editor.Logger
{
    [System.Serializable]
    public class Logger
    {
        public static void LogDebug(string message, bool useFlag = true)
        {
            if (!useFlag) return;
            
            Debug.Log($"[Debug] {message}");
        }

        public static void LogInfo(string message, bool useFlag = true)
        {
            if (!useFlag) return;
            
            Debug.Log($"[Info] {message}");
        }

        public static void LogWarning(string message)
        {
            Debug.LogWarning($"[Warning] {message}");
        }

        public static void LogError(string message)
        {
            Debug.LogError($"[Error] {message}");
        }
    }
}