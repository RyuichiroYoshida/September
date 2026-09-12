using System.Linq;
using Common.Extensions;
using Cysharp.Threading.Tasks;
using Fusion;
using InGame.Common;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;

namespace InGame.Player.Ult
{
    /// <summary>
    /// 必殺技用カットインの実装クラス
    /// </summary>
    [RequireComponent(typeof(AnimationClipPlayer))]
    public class UltCutInAnimator : CutInAnimatorBase
    {
        [SerializeField] private PlayableDirector _playableDirector;
        [SerializeField] private CinemachineVirtualCameraBase _camera;

        public override double Duration
        {
            get
            {
                if (!_playableDirector) return 0;
                return _playableDirector.duration;
            }
        }

        public override void RequestPlayCutInAnimation()
        {
            if (!_playableDirector)
            {
                Debug.LogWarning("[UltCutInAnimator] PlayableDirectorが設定されていません。必殺技カットインはスキップされます");
                return;
            }
            
            RPC_PlayCutInAnimation();
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_PlayCutInAnimation()
        {
            // 発動者かどうかで演出を振り分け
            if (HasInputAuthority)
            {
                PlayLocal().Forget();
            }
            else
            {
                PlayRemote().Forget();
            }
        }

        /// <summary>
        /// 発動者以外が見る演出
        /// </summary>
        private async UniTask PlayRemote()
        {
            await _playableDirector.PlayAsync();
        }

        /// <summary>
        /// 発動者が見る演出
        /// </summary>
        private async UniTask PlayLocal()
        {
            // フィールド上にあるCinemachine Brainを動的バインド
            PlayableBinding binding = _playableDirector.playableAsset.outputs.FirstOrDefault(c => c.streamName == "Cinemachine Track");
            if (binding.streamName == "Cinemachine Track")
            {
                _playableDirector.SetGenericBinding(binding.sourceObject, FindFirstObjectByType<CinemachineBrain>());
            }

            if (_camera) _camera.gameObject.SetActive(true);
            
            await _playableDirector.PlayAsync();
            
            if (_camera) _camera.gameObject.SetActive(false);
        }
    }
}
