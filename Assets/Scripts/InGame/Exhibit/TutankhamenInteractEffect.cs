using System;
using CRISound;
using Cysharp.Threading.Tasks;
using Fusion;
using InGame.Interact;
using InGame.Player;
using September.Common;
using September.InGame.Effect;
using UnityEngine;
using Object = UnityEngine.Object;

namespace InGame.Exhibit
{
    [Serializable]
    public class TutankhamenInteractEffect : CharacterInteractEffectBase
    {
        public ParticleSystem _tutankhamenHead;
        public string _soundName;
        public GameObject _effectPos;
        public float _maskDuration = 5f;
        public float _boostMultiplier = 1.5f;

        private bool _isDestroyScheduled;
        private EffectSpawner _effectSpawner;
        private ParticleSystem _instantiateMask;
        private bool _isMaskAttached;
        
        private float _originalSpeedRate = -1f;
        private NetworkObject _targetPlayerObject;

        public override void OnInteractStart(IInteractableContext context, InteractableBase target)
        {
            _isDestroyScheduled = false;
            PlayerRef playerRef = PlayerRef.FromEncoded(context.Interactor);
            
            // Runnerからplayerを取得する
            if (target.Runner.TryGetPlayerObject(playerRef, out var playerNetworkObject))
            {
                var playerStatus = playerNetworkObject.GetComponent<PlayerStatus>();
                if (playerStatus != null && playerStatus.HasStateAuthority)
                {
                    _originalSpeedRate = playerStatus.MaxSpeedRate;
                    playerStatus.MaxSpeedRate = playerStatus.MaxSpeedRate * _boostMultiplier;
                    Debug.Log($"Boosted SpeedRate: {playerStatus.MaxSpeedRate}");
                }

                _targetPlayerObject = playerNetworkObject; // 後で戻す対象を保持
                
                PlayEffect(playerNetworkObject,target.transform.position);
            }
        }

        public override void OnInteractUpdate(float deltaTime)
        {
            if (!_isDestroyScheduled && _isMaskAttached)
            {
                _isDestroyScheduled = true;
                DestroyMask().Forget();
            }
        }

        public override CharacterInteractEffectBase Clone()
        {
            return new TutankhamenInteractEffect
            {
                _soundName = _soundName , 
                _tutankhamenHead = _tutankhamenHead,
                _effectPos = _effectPos,
                _maskDuration = _maskDuration,
                _boostMultiplier = _boostMultiplier,
            };
        }

        private async UniTaskVoid DestroyMask()
        {
            await UniTask.Delay(TimeSpan.FromSeconds(_maskDuration));

            if (_instantiateMask != null)
            {
                Object.Destroy(_instantiateMask.gameObject);
                _instantiateMask = null;
            }
            _isMaskAttached = false;

            // 🧠 ステータスを元に戻す処理
            if (_targetPlayerObject != null && _targetPlayerObject.TryGetComponent(out PlayerStatus status))
            {
                if (status.HasStateAuthority && _originalSpeedRate >= 0f)
                {
                    status.MaxSpeedRate = _originalSpeedRate;
                    Debug.Log($"Restored SpeedRate: {status.MaxSpeedRate}");
                }
            }

            _targetPlayerObject = null;
            _originalSpeedRate = -1f;
        }

        // 非同期でAnimationを再生する
        private void PlayEffect(NetworkObject player,Vector3 targetPos)
        {
            // Effect生成処理
            _effectSpawner ??= StaticServiceLocator.Instance.Get<EffectSpawner>();
            
            _effectSpawner?.RequestPlayOneShotEffect(EffectType.Tutankhamen, _effectPos.transform.position,
                _effectPos.transform.rotation,_effectPos.transform);
            // 音量再生
            CRIAudio.PlaySE("Exhibit",_soundName);
            AttachHeadMask(player);
        }

        // 仮面をかぶる処理
        private void AttachHeadMask(NetworkObject player)
        {
            if(_tutankhamenHead == null)
                return;
            
            // Playerの頭の位置を探す
            Transform head = player.transform.Find("Head");
            if (head == null)
            {
                Debug.LogError("AttachHeadMask head is null");
                return;
            }
            
            // 仮面をインスタンスして頭の子にする
            _instantiateMask = Object.Instantiate(_tutankhamenHead, head);
            _instantiateMask.transform.localPosition = Vector3.zero;
            _instantiateMask.transform.localRotation = Quaternion.identity;
            
            _instantiateMask.Play();
            _isMaskAttached = true;
        }
    }
}