using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Fusion;
using InGame.Health;
using September.Common;
using September.InGame.Common.Stats;
using September.InGame.Rules;
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
        public int CurrentHealth => _status.CurrentHealth;

        public override void Spawned()
        {
            if (HasStateAuthority)
            {
                OnDeath += Death;
            }
            
            _status = GetComponent<PlayerStatus>();
            _cts = new CancellationTokenSource();
            _renderer = GetComponentInChildren<Renderer>();
            _status = GetComponent<PlayerStatus>();
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
                Debug.Log($"PlayerHealth: TakeHit - HitActionType: {hitData.HitActionType}, Amount: {hitData.Amount}, IsLastHit: {hitData.IsLastHit}");
                if (!IsAlive) OnDeath?.Invoke(hitData);
                hitData.Executor?.HitExecution(hitData);

                IGameRule.CurrentRule.PlayerHitStrategy?.OnHitTaken(ref hitData);

                PlayerDatabase.Instance.Server_AddDamageDealt(hitData.ExecutorRef, hitData.Amount);
                PlayerDatabase.Instance.Server_AddDamageReceived(hitData.TargetRef, hitData.Amount);
            }
            
            //RPC_HitDebug(hitData.HitActionType);
        }

        void ApplyHit(ref HitData hitData)
        {
            if (!IsAlive)
            {
                hitData.Amount = 0;
                return;
            }

            if (hitData.HitActionType.IsDamage())
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
            _status.AddBaseValue(StatType.Health, -damage);
            return previousHealth - _status.CurrentHealth;
        }

        int TakeHeal(int heal)
        {
            if (IsInvincible) return 0;
            int previousHealth = _status.CurrentHealth;
            _status.AddBaseValue(StatType.Health, heal);
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
            _materialPropertyBlock.SetColor("_BaseColor", actionType.IsDamage() ? Color.red : Color.green);
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
            _status.SetBaseValue(StatType.Health, _status.MaxHealth);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            OnHitTaken = null;
            Debug.Log("PlayerHealth: Despawned - OnHitTaken event handlers cleared");
            OnDeath = null;
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}
