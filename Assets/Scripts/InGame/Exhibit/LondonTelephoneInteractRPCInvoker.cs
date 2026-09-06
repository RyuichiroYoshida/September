using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Fusion;
using InGame.Interact;
using NaughtyAttributes;
using September.Common;
using September.InGame.Effect;
using UnityEngine;

namespace InGame.Exhibit
{
    /// <summary>
    /// ロンドンテレフォンインタラクトRPC送信処理
    /// </summary>
    public class LondonTelephoneInteractRPCInvoker : NetworkBehaviour
    {
        [SerializeField, Label("Effect持続時間")] private float _interactTimer;
        [SerializeField, Label("エフェクトの位置")] private Transform _effectPos;

        private EffectSpawner _effectSpawner;
        private CancellationTokenSource _cts;
        private InteractableBase _interactableBase;

        private void Awake()
        {
            _cts = new CancellationTokenSource();
            _interactableBase = GetComponent<InteractableBase>();
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void Rpc_RequestInteraction(PlayerRef requestingPlayer)
        {
            // ここで他のプレイヤーに通知
            foreach (var kv in PlayerDatabase.Instance.PlayerObjectDic)
            {
                var player = kv.Key;
                if (player != requestingPlayer)
                {
                    ShowEffect(player).Forget();
                }
            }
        }

        private async UniTask ShowEffect(PlayerRef player)
        {
            if (!PlayerDatabase.Instance.PlayerObjectDic.TryGet(player, out var playerObject))
                return;

            // 同時インタラクト不可
            _interactableBase.ForceSetInteractable = false;
            Vector3 effectPosition = playerObject.transform.position;

            // Effect生成処理
            InitEffectSpawner();

            if (_effectSpawner == null) return;

            // ロンドンテレフォンの下にエフェクトを再生
            Vector3 startPos = _effectPos != null ? _effectPos.position : transform.position;
            _effectSpawner.RequestPlayOneShotEffect(
                EffectType.LondonTelephoneStart,
                startPos,
                Quaternion.identity
            );

            EffectID effectId = _effectSpawner.RequestPlayLoopEffect(
                EffectType.LondonTelephoneActive,
                effectPosition + Vector3.up,
                new Quaternion(),
                playerObject.transform
            );

            // Effectを消す
            await UniTask.Delay(TimeSpan.FromSeconds(_interactTimer), cancellationToken: _cts.Token);
            _effectSpawner?.StopEffect(effectId);
            _interactableBase.ForceSetInteractable = true;
        }

        private void InitEffectSpawner()
        {
            _effectSpawner ??= StaticServiceLocator.Instance.Get<EffectSpawner>();
        }
    }
}
