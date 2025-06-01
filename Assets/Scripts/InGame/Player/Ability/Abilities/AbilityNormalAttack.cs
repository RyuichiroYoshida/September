using System.Collections.Generic;
using InGame.Health;
using UnityEngine;
using Fusion;
using NaughtyAttributes;
using September.Common;
using September.InGame.Common;

namespace InGame.Player.Ability
{
    [System.Serializable]
    public class AbilityNormalAttack : AbilityBase
    {
        [SerializeField] private float _attackRadius = 1.0f;
        [SerializeField, Label("攻撃力")] private int _attackDamage = 10;
        [SerializeField] private float _attackDuration = 1.0f; // 維持時間
        [SerializeField] private LayerMask _hitMask;
        private readonly Collider[] _hitBuffer = new Collider[10];
        private IHitExecutor _hitExecutor;
        private static InGameManager _inGameManager;

        private float _remainingTime;
        private Vector3 _attackOrigin;
        private readonly HashSet<PlayerHealth> _alreadyHit = new();

        public override bool RunLocal => false;
        public override string DisplayName => "通常攻撃";

        public AbilityNormalAttack() { }

        public AbilityNormalAttack(AbilityBase abilityReference) : base(abilityReference) { }

        public override AbilityBase Clone(AbilityBase abilityReference) => new AbilityNormalAttack(this);

        protected override void OnStart()
        {
            if (!_inGameManager && !StaticServiceLocator.Instance.TryGet(out _inGameManager))
            {
                Debug.LogError("InGameManagerが見つかりません。通常攻撃を実行できません。");
                ForceEnd();
                return;
            }

            if (!_inGameManager.PlayerDataDic.TryGetValue(PlayerRef.FromEncoded(Context.SourcePlayer), out var playerData))
            {
                Debug.LogError("PlayerDataが見つかりません。通常攻撃を実行できません。");
                ForceEnd();
                return;
            }

            _attackOrigin = playerData.transform.position;
            _remainingTime = _attackDuration;
            _alreadyHit.Clear();

#if UNITY_EDITOR
            DebugDrawHelper.RegisterAttackPosition(_attackOrigin, _attackRadius, Color.red, _attackDuration);
#endif
        }

        protected override void OnUpdate(float deltaTime)
        {
            _remainingTime -= deltaTime;
            if (_remainingTime <= 0f)
            {
                ForceEnd();
                return;
            }

            int hitCount = Physics.OverlapSphereNonAlloc(_attackOrigin, _attackRadius, _hitBuffer, _hitMask);

            for (var i = 0; i < hitCount; i++)
            {
                var hit = _hitBuffer[i];
                var target = hit.GetComponent<PlayerHealth>();

                if (target == null || target.HasInputAuthority || _alreadyHit.Contains(target))
                    continue;

                if (!StaticServiceLocator.Instance.TryGet<IHitExecutor>(out var executor))
                {
                    Debug.LogError("IHitExecutorが見つかりません。");
                    continue;
                }

                var hitData = new HitData
                {
                    HitActionType = HitActionType.Damage,
                    Amount = _attackDamage,
                    ExecutorRef = PlayerRef.FromEncoded(Context.SourcePlayer),
                    TargetRef = target.Object.InputAuthority,
                    Target = target,
                    Executor = executor,
                };

                target.TakeHit(ref hitData);
                _alreadyHit.Add(target);
            }
        }
    }
}
