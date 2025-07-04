using System;
using CRISound;
using Cysharp.Threading.Tasks;
using Fusion;
using InGame.Interact;
using September.Common;
using September.InGame.Effect;
using UnityEngine;
using Object = UnityEngine.Object;

namespace InGame.Exhibit
{
    [Serializable]
    public class TutankhamenObject : CharacterInteractEffectBase
    {
        public GameObject _tutankhamenHead;
        public string _soundName;
        public GameObject _effectPos;
        public float _maskDuration = 5f;
        public float _boostMultiplier = 1.5f;

        private bool _isDestroyScheduled;
        private EffectSpawner _effectSpawner;
        private GameObject _instantiateMask;
        private bool _isMaskAttached;

        public override void OnInteractStart(IInteractableContext context, InteractableBase target)
        {
            _isDestroyScheduled = false;
            PlayerRef playerRef = PlayerRef.FromEncoded(context.Interactor);
            
            // ToDo : パラメーターを書き換える
            
            // Runnerからplayerを取得する
            if (target.Runner.TryGetPlayerObject(playerRef, out var playerNetworkObject))
            {
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
            return new TutankhamenObject
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
                Object.Destroy(_instantiateMask);
                _instantiateMask = null;
            }
            _isMaskAttached = false;
            
        }

        // 非同期でAnimationを再生する
        private void PlayEffect(NetworkObject player,Vector3 targetPos)
        {
            // Effect生成処理
            _effectSpawner ??= StaticServiceLocator.Instance.Get<EffectSpawner>();
            
            Vector3 effectPos = _effectPos.transform.position + targetPos;
            Debug.Log("Playing effect: " + effectPos);
            _effectSpawner?.RequestPlayOneShotEffect(EffectType.Tutankhamen, effectPos, _effectPos.transform.rotation);
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
            
            // カメラをインスタンス化して頭の子にする
            _instantiateMask = Object.Instantiate(_tutankhamenHead, head.transform);
            _instantiateMask.transform.localPosition = Vector3.zero;
            _instantiateMask.transform.localRotation = Quaternion.identity;
            _isMaskAttached = true;
        }
    }
}