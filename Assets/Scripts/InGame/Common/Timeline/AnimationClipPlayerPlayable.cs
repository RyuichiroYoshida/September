using UnityEngine;
using UnityEngine.Playables;

namespace InGame.Common.Timeline
{
    [System.Serializable]
    public class AnimationClipPlayerPlayable : PlayableBehaviour
    {
        [SerializeField] private AnimationClip _clip;
        
        private bool _isPlayed;
        private AnimationClipPlayer.PlayableInfo _info;

        private AnimationClipPlayer _clipPlayer;
        
        public AnimationClip Clip { get => _clip; set => _clip = value; }

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (!_clipPlayer)
            {
                _clipPlayer = playerData as AnimationClipPlayer;
                
#if UNITY_EDITOR
                if (!Application.isPlaying && _clipPlayer && !_clipPlayer.IsValid)
                {
                    _clipPlayer.Start();
                }
#endif
            }
            
            if (_clipPlayer == null || _clip == null) return;

            // 未再生の場合、再生を開始する
            if (!_isPlayed)
            {
                _clipPlayer.Play(_clip);

                // 生成されたPlayableを取得する
                if (!_clipPlayer.TryGetPlayableInfo(_clip, out _info))
                {
                    Debug.LogWarning($"AnimationClipPlayerPlayable: AnimationClipPlayable is not found (clip:{_clip.name}, _clipPlayer:{_clipPlayer})");
                    return;
                }

                // 既に再生済みとしてマーク
                _isPlayed = true;
            }

            // Playableが既に破棄されているかチェック
            if (!_info.playable.IsValid()) return;

            var time = playable.GetTime();
            
            _info.SetTime(Mathf.Clamp((float)time, 0f, _clip.length), false);
            _info.SetBlendTime((float)time, (float)playable.GetDuration());

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                _clipPlayer.Update();
                _clipPlayer.LateUpdate();
            }
#endif
        }

        public override void OnBehaviourPause(Playable playable, FrameData info)
        {
            _info.Disconnect();
            _isPlayed = false;
        }
    }
}
