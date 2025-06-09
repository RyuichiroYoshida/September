using UnityEngine;

namespace Editor.Logger
{
    public class Logger
    {
        public void LogDebug(string message, bool useFlag = true)
        {
            if (!useFlag) return;
            
            Debug.Log($"[Debug] {message}");
        }

        public void LogInfo(string message, bool useFlag = true)
        {
            if (!useFlag) return;
            
            Debug.Log($"[Info] {message}");
        }

        public void LogWarning(string message)
        {
            Debug.LogWarning($"[Warning] {message}");
        }

        public void LogError(string message)
        {
            Debug.LogError($"[Error] {message}");
        }
    }
}