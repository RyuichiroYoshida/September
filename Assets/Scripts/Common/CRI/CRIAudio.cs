using UnityEngine;

namespace CRISound
{
    public static class CRIAudio
    {
        public static void PlayBGM(string cueSheet, string cueName) =>
            CuePlayAtomExPlayer.Instance.Player(SoundType.BGM).Play(cueSheet, cueName);

        public static void StopBGM(string cueSheet, string cueName) =>
            CuePlayAtomExPlayer.Instance.Player(SoundType.BGM).Stop();
        
        public static void PlaySE(string cueSheet, string cueName) =>
        CuePlayAtomExPlayer.Instance.Player(SoundType.SE).Play(cueSheet, cueName);
        
        public static void PlayVoice(string cueSheet, string cueName) =>
        CuePlayAtomExPlayer.Instance.Player(SoundType.Voice).Play(cueSheet, cueName);
        
        public static void PlaySE(Vector3 pos, string cueSheet, string cueName) =>
        CuePlayAtomExPlayer.SE.Play3D(pos, cueSheet, cueName);
    }
}