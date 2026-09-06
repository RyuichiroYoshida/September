using UnityEngine;

namespace September.Common.Performance
{
    /// <summary>
    /// 製品ビルドで Debug.Log / Debug.LogWarning のスタックトレース取得を止める。
    /// <para>
    /// ProjectSettings の m_StackTraceTypes は全ログ種別が ScriptOnly になっており、
    /// ログ 1 行ごとにマネージドスタックの文字列化が走る。ランタイムコードには
    /// Debug.Log 系が 370 箇所以上あり、毎フレームに近い頻度で通る経路も含まれるため、
    /// 製品ビルドでは取得を止める。
    /// </para>
    /// <para>
    /// Editor と Development Build では、ログからソース行へ辿れる利便性を優先して
    /// 既定 (ScriptOnly) のまま残す。Error / Assert / Exception は原因追跡に必須なので
    /// 製品ビルドでも変更しない。
    /// </para>
    /// </summary>
    public static class LogStackTraceSettings
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
#endif
        }
    }
}
