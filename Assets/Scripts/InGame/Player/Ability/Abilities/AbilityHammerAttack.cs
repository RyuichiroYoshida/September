using System.Collections.Generic;
using Fusion;
using InGame.Health;
using September.Common;
using September.InGame.Common;
using UnityEngine;

namespace InGame.Player.Ability
{
    /// <summary>
    /// ハンマー攻撃のアビリティ
    /// </summary>
    /// <remarks>
    /// このクラスはハンマー攻撃のアビリティを表現します。
    /// </remarks>
    [System.Serializable]
    public class AbilityHammerAttack : AbilityBase
    {
        [SerializeField] private float _attackRadius = 2.0f;
        [SerializeField] private float _attackAngle = 90f;
        [SerializeField] private float _attackDelay = 0.4f;
        [SerializeField] private LayerMask _hitMask;
        [SerializeField] private Transform _attackOriginTransform;

        private static InGameManager _inGameManager;

        private float _timer;
        private bool _hasAttacked;
        private const int StunDamage = 10000;

        private readonly Collider[] _hitBuffer = new Collider[16];
        private readonly HashSet<IDamageable> _alreadyHit = new();

        public override bool RunLocal => false;
        public override string DisplayName => "ハンマー攻撃";

        public AbilityHammerAttack()
        {
        }

        public AbilityHammerAttack(AbilityBase abilityReference) : base(abilityReference)
        {
        }

        public override AbilityBase Clone(AbilityBase abilityReference) => new AbilityHammerAttack(this);

        protected override void OnStart()
        {
            if (!_inGameManager && !StaticServiceLocator.Instance.TryGet(out _inGameManager))
            {
                Debug.LogError("InGameManagerが見つかりません。ハンマー攻撃を実行できません。");
                ForceEnd();
                return;
            }

            _timer = 0f;
            _hasAttacked = false;
            _alreadyHit.Clear();

            PlayAnimation("HammerAttack");

#if UNITY_EDITOR
            DebugDrawHelper.RegisterAttackPosition(_attackOriginTransform.position, _attackRadius, Color.yellow, 1.0f);
#endif
        }

        protected override void OnUpdate(float deltaTime)
        {
            _timer += deltaTime;

            if (!_hasAttacked && _timer >= _attackDelay)
            {
                _hasAttacked = true;

                AttackHitUtility.OverlapDamageables(
                    _attackOriginTransform.position,
                    _attackRadius,
                    _hitBuffer,
                    _alreadyHit,
                    OwnerPlayerId,
                    _hitMask,
                    _attackAngle,
                    _attackOriginTransform.forward
                );

                ApplyHammerHits();
            }

            if (_timer >= _attackDelay + 0.3f)
            {
                ForceEnd();
            }
        }

        private void ApplyHammerHits()
        {
            
            foreach (var target in _alreadyHit)
            {
                var hitData = new HitData
                {
                    HitActionType = HitActionType.Damage,
                    Amount = StunDamage,
                    ExecutorRef = PlayerRef.FromEncoded(Context.SourcePlayer),
                    TargetRef = target.OwnerPlayerRef,
                    Target = target,
                };
                target.TakeHit(ref hitData); // プレイヤーに一撃スタン
            }

            _alreadyHit.Clear();
        }

        private void PlayAnimation(string animName)
        {
            // Animator連携処理（例：_animator.Play(animName)）
        }
    }
}