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
        public bool IsAlive => Health > 0;
        public readonly BehaviorSubject<int> OnHealthChanged = new(0);
        public PlayerRef OwnerPlayerRef => Object.InputAuthority;
        
        // event
        public event Action<HitData> OnHitTaken;
        public event Action<HitData> OnDeath;

        [Networked, OnChangedRender(nameof(OnChangeHealth))] private int Health { get; set; }
        void OnChangeHealth() => OnHealthChanged.OnNext(Health);
        [Networked, HideInInspector] public int MaxHealth { get; private set; }
        /// <summary> 無敵 </summary> 無敵の set が　public なのどうなん
        [Networked, HideInInspector] public NetworkBool IsInvincible { get; set; }

        private CancellationTokenSource _cts;
        
        Renderer _renderer;
        MaterialPropertyBlock _materialPropertyBlock;

        public void Init(int health)
        {
            if (HasStateAuthority)
            {
                Health = health;
                MaxHealth = health;
            
                OnHealthChanged.OnNext(Health);

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
            RPC_HitDebug();
            Debug.Log(hitData + $"\nHealth:     {Health}");
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
        }

        int TakeDamage(int damage)
        {
            if (IsInvincible) return 0;
            int previousHealth = Health;
            Health  = Mathf.Clamp(Health - damage, 0, MaxHealth);
            return previousHealth - Health;
        }


        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_HitDebug()
        {
            HitDebug().Forget();
        }
        

        private async UniTask HitDebug()
        {
            _renderer.GetPropertyBlock(_materialPropertyBlock);
            _materialPropertyBlock.SetColor("_BaseColor", Color.red);
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
            Health = MaxHealth;
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
