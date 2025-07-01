#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

public static class HitboxDebugUtility
{
    private static bool _debugMode = false;

    public static bool IsDebugModeEnabled => _debugMode;

#if UNITY_EDITOR
    [MenuItem("Debug/Toggle Hitbox Debug %h")]
    public static void ToggleHitboxDebug()
    {
        _debugMode = !_debugMode;
        Debug.Log($"[HitboxDebug] Debug Mode: {_debugMode}");
        SceneView.RepaintAll(); // 描画即時更新
    }
#endif
}