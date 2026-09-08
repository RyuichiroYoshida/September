using Fusion;
using InGame.Common;
using InGame.Health;
using InGame.Exhibit.HazardTrail;
using September.Common;
using September.InGame.Common.Stats;
using September.InGame.Effect;
using System.Collections.Generic;
using UnityEngine;

namespace InGame.Exhibit.Candle
{
    /// <summary>
    /// 蝋燭ギミックの本体ロジックを管理するネットワークコンポーネント。
    /// インタラクトしたプレイヤーに一定時間「火だるま状態」を付与し、
    /// 移動速度バフ、周囲へのオーラ持続ダメージ、および移動軌跡へのハザード設置（トレイル）を統括します。
    /// </summary>
    public class CandleInteractInvoker : NetworkBehaviour
    {
        [Header("火だるま攻撃設定")]
        [SerializeField] private float _attackRadius = 2.0f;
        [SerializeField] private int _auraDamage = 1;
        [SerializeField] private float _auraInterval = 0.25f;
        [SerializeField] private Vector3 _auraOffset = new Vector3(0, 0.5f, 0);
        [SerializeField] private LayerMask _targetLayer;

        [Header("持続時間・トレイル")]
        [SerializeField] private HazardTrailEmitter _trailEmitter;
        [SerializeField] private StatusEffect _speedBuffEffect;
        [SerializeField] private float _duration = 8.0f;
        [SerializeField] private float _speedMultiplier = 1.3f;

        [Networked] public TickTimer DurationTimer { get; set; }
        [Networked] public TickTimer AuraAttackTimer { get; set; }
        private PlayerRef _currentOwner;
        private Transform _targetTransform;
        private readonly Collider[] _hitColliders = new Collider[10];
        private readonly HashSet<IDamageable> _damagedTargets = new HashSet<IDamageable>();
        private EffectSpawner EffectSpawner => StaticServiceLocator.Instance.Get<EffectSpawner>();
        private EffectID _currentEffectId;

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority || _currentOwner.IsNone) return;

            if (_targetTransform == null)
            {
                StopAttack();
                return;
            }
            // トレイル生成の更新
            if (_trailEmitter != null)
            {
                _trailEmitter.UpdateEmitter();
            }
            //オーラ持続ダメージ
            if (AuraAttackTimer.ExpiredOrNotRunning(Runner))
            {
                PerformAuraAttack();
                AuraAttackTimer = TickTimer.CreateFromSeconds(Runner, _auraInterval);
            }
            // 効果時間終了
            if (DurationTimer.Expired(Runner))
            {
                StopAttack();
            }
        }

        public void StartAttack(int interactor)
        {
            if (!_currentOwner.IsNone) return;
            var playerRef = PlayerRef.FromEncoded(interactor);
            if (!PlayerDatabase.Instance.PlayerObjectDic.TryGet(playerRef, out var playerObj)) return;

            _currentOwner = playerRef;
            _targetTransform = playerObj.transform;

            // エフェクト生成
            _currentEffectId = EffectSpawner.RequestPlayLoopEffect(
            EffectType.CandleAura,
            playerObj.transform.position + _auraOffset,
            Quaternion.identity,
            playerObj.transform 
            );
            //移動速度バフ付与
            if (playerObj.TryGetComponent<StatusEffectManager>(out var sem))
            {
                var spec = new StatusEffectManager.StatusEffectSpec(_speedBuffEffect) { Duration = _duration };
                spec.Modifiers[0].SetByCallerMagnitude(_speedMultiplier);
                sem.AddEffect(spec);
            }

            // トレイル生成開始
            if (_trailEmitter != null)
            {
                _trailEmitter.StartEmitting(_targetTransform, _currentOwner);
            }   

            // タイマーの起動
            DurationTimer = TickTimer.CreateFromSeconds(Runner, _duration);
            AuraAttackTimer = TickTimer.CreateFromSeconds(Runner, _auraInterval);
        }

        private void PerformAuraAttack()
        {
            if (_targetTransform == null) return;
            Vector3 center = _targetTransform.position + _auraOffset;
            int hitCount = Physics.OverlapSphereNonAlloc(center, _attackRadius, _hitColliders, _targetLayer);
            _damagedTargets.Clear();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // 攻撃判定が発生した瞬間にデバッグ描画
            HitboxDebugUtility.DrawWireSphere(center, _attackRadius, Color.red, _auraInterval);
#endif

            for (int i = 0; i < hitCount; i++)
            {
                var col = _hitColliders[i];
                if (col == null) continue;
                var root = col.transform.root;
                if (root.TryGetComponent<IDamageable>(out var damageable) && damageable.OwnerPlayerRef != _currentOwner)
                {
                    // 同一Tick内で多重ダメージが入らないように重複排除
                    if (_damagedTargets.Add(damageable))
                    {
                        var hitData = new HitData(HitActionType.Damage, _auraDamage, _currentOwner, damageable.OwnerPlayerRef);
                        damageable.TakeHit(ref hitData);
                    }
                }
            }
        }

        private void StopAttack()
        {
            if (_currentOwner.IsNone) return;

            EffectSpawner?.StopEffectGradually(_currentEffectId);
            _trailEmitter?.StopEmitting();

            _currentOwner = PlayerRef.None;
            _targetTransform = null;
        }
        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            // オブジェクト破棄時のエフェクト・トレイル残留防止
            StopAttack();
            base.Despawned(runner, hasState);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Vector3 center = _targetTransform != null
                ? _targetTransform.position + _auraOffset
                : transform.position + _auraOffset;
            Gizmos.DrawWireSphere(center, _attackRadius);
        }
#endif
    }
}
