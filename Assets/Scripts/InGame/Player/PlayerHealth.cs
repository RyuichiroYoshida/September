using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Fusion;
using InGame.Health;
using UniRx;
using UnityEngine;

namespace InGame.Player
{
    public class PlayerHealth : NetworkBehaviour, IDamageable
    {
        PlayerStatus _status;
        private CancellationTokenSource _cts;
        Renderer _renderer;
        MaterialPropertyBlock _materialPropertyBlock;
        
        public bool IsAlive => _status.CurrentHealth > 0;
        public PlayerRef OwnerPlayerRef => Object.InputAuthority;
        
        // event
        public event Action<HitData> OnHitTaken;
        public event Action<HitData> OnDeath;

        /// <summary> 無敵 </summary> 無敵の set が　public なのどうなん
        [Networked, HideInInspector] public NetworkBool IsInvincible { get; set; }

        public override void Spawned()
        {
            if (HasStateAuthority)
            {
                OnDeath += Death;
            }
            
            _cts = new CancellationTokenSource();
            _renderer = GetComponentInChildren<Renderer>();
            _materialPropertyBlock = new MaterialPropertyBlock();
        }

        public void TakeHit(ref HitData hitData)
        {
            ApplyHit(ref hitData);

            hitData.IsLastHit = !IsAlive;

            if (HasStateAuthority)
            {
                // イベントの発火はStateAuthorityなのか？
                OnHitTaken?.Invoke(hitData);
                if (!IsAlive) OnDeath?.Invoke(hitData);
                hitData.Executor?.HitExecution(hitData);
            }
            
            RPC_HitDebug(hitData.HitActionType);
            Debug.Log(hitData + $"\nHealth:     {_status.CurrentHealth}");
        }

        void ApplyHit(ref HitData hitData)
        {
            if (!IsAlive)
            {
                hitData.Amount = 0;
                return;
            }

            if (hitData.HitActionType == HitActionType.Damage)
            {
                hitData.Amount = TakeDamage(hitData.Amount);
            }
            else if (hitData.HitActionType == HitActionType.Heal)
            {
                hitData.Amount = TakeHeal(hitData.Amount);
            }
        }

        int TakeDamage(int damage)
        {
            if (IsInvincible) return 0;
            int previousHealth = _status.CurrentHealth;
            _status.CurrentHealth = Mathf.Clamp(_status.CurrentHealth - damage, 0, _status.MaxHealth);
            return previousHealth - _status.CurrentHealth;
        }

        int TakeHeal(int heal)
        {
            if (IsInvincible) return 0;
            int previousHealth = _status.CurrentHealth;
            _status.CurrentHealth = Mathf.Clamp(_status.CurrentHealth + heal, 0, _status.MaxHealth);
            return _status.CurrentHealth - previousHealth;
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_HitDebug(HitActionType actionType)
        {
            HitDebug(actionType).Forget();
        }
        

        private async UniTask HitDebug(HitActionType actionType)
        {
            _renderer.GetPropertyBlock(_materialPropertyBlock);
            _materialPropertyBlock.SetColor("_BaseColor", actionType == HitActionType.Damage ? Color.red : Color.green);
            _renderer.SetPropertyBlock(_materialPropertyBlock);
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(0.1f), cancellationToken: _cts.Token);
            }
            catch(OperationCanceledException) { }
            _renderer.GetPropertyBlock(_materialPropertyBlock);
            _materialPropertyBlock.SetColor("_BaseColor",Color.white);
            _renderer.SetPropertyBlock(_materialPropertyBlock);
        }

        /// <summary> 死んだとき </summary>
        void Death(HitData lastHitData)
        {
            _status.CurrentHealth = _status.MaxHealth;
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            OnHitTaken = null;
            OnDeath = null;
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}
