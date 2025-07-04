#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using CriWare;

namespace CRISound
{
    [InitializeOnLoad]
    public static class CRIEditorSettings
    {
        private static bool _lastPauseState = false;

        static CRIEditorSettings()
        {
            EditorApplication.update += OnEditorUpdate;
        }

        private static void OnEditorUpdate()
        {
            if(!EditorApplication.isPlaying)
                return;
            
            bool isNowPaused = EditorApplication.isPaused;

            if (_lastPauseState != isNowPaused)
            {
                _lastPauseState = isNowPaused;
                
                CriAtomExAsr.PauseOutputVoice(isNowPaused);
            }
        }
    }
#endif
}