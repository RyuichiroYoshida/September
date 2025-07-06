using System;
using System.Threading;
using CRISound;
using Cysharp.Threading.Tasks;
using Fusion;
using NaughtyAttributes;
using UnityEngine;
using InGame.Interact;
using InGame.Player;
using September.InGame.Effect;

namespace InGame.Exhibit
{
    [Serializable]
    public class WarpInteractEffect : CharacterInteractEffectBase
    {
        [Label("ワープ先（Goal）")] 
        public GameObject _warpDestination;
        [Label("Duration")] 
        public float _warpDuration = 0.5f;
        [Label("ワープ先のワープポジション")]
        public GameObject _warpPosition;
        [Label("音名")] 
        public string _warpInSoundName;

        public string _warpOutSoundName;
        
        private CancellationTokenSource _cts;

        private EffectSpawner _effectSpawner;
        
        public override CharacterInteractEffectBase Clone()
        {
            return new WarpInteractEffect
            {
                _warpDestination = _warpDestination,
                _warpDuration = _warpDuration,
                _warpPosition = _warpPosition,
                _warpInSoundName = _warpInSoundName,
                _warpOutSoundName = _warpOutSoundName,
            };
        }
        
        public override void OnInteractStart(IInteractableContext context, InteractableBase target)
        {
            _cts = new CancellationTokenSource();
            PlayerRef playerRef = PlayerRef.FromEncoded(context.Interactor);
            if(target.Runner.TryGetPlayerObject(playerRef, out NetworkObject playerNetworkObject))
            {
                HandleWarpAsync(playerNetworkObject).Forget();
            }
        }

        // インタラクト時のWarp処理
        private async UniTaskVoid HandleWarpAsync(NetworkObject player)
        {
            // Effectの再生
            Vector3 effectPos = player.transform.position + Vector3.up * 1.0f;
            PlayEffect(EffectType.Warp, effectPos,Quaternion.identity);
            PlaySE(_warpInSoundName);

            // Playerを初期化
            SetPlayerVisible(player, false);
            Vector3 targetPos = _warpPosition.transform.position;
            Vector3 backward = _warpDestination.transform.forward;
            Quaternion targetRot = Quaternion.LookRotation(backward,Vector3.up);
            
            // NetWork経由で移動を指示
            PlayerManager playerManager = player.GetComponent<PlayerManager>();
            playerManager?.SetWarpTarget(targetPos, targetRot);
            
            // 少し待ってから移動予約
            await UniTask.Delay(TimeSpan.FromSeconds(_warpDuration),cancellationToken: _cts.Token);
            
            // ゴール側エフェクトの再生
            PlayEffect(EffectType.Warp, targetPos, Quaternion.identity);
            SetPlayerVisible(player, true);
            PlaySE(_warpOutSoundName);
        }

        private void SetPlayerVisible(NetworkObject player, bool isVisible)
        {
            // Playerオブジェクトのどっかから拾ってくる
            foreach (Renderer renderer in player.GetComponentsInChildren<Renderer>())
            {
                renderer.enabled = isVisible;
            }
        }

        private void PlaySE(string soundName)
        {
            CRIAudio.PlaySE("Exhibit", soundName);
        }

        private void PlayEffect(EffectType effectType,Vector3 effectPos,Quaternion effectRot)
        {
            _effectSpawner?.RequestPlayOneShotEffect(effectType, effectPos, effectRot);
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
    }
}