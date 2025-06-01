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
        [SerializeField] private LayerMask _hitMask;         
        private readonly Collider[] _hitBuffer = new Collider[10]; 
        private IHitExecutor _hitExecutor;
        private static InGameManager _inGameManager;

        public override bool RunLocal => false;
        public override string DisplayName => "通常攻撃";
        
        public AbilityNormalAttack() { }
        public AbilityNormalAttack(AbilityBase abilityReference) : base(abilityReference) { }
        public override AbilityBase Clone(AbilityBase abilityReference) => new AbilityNormalAttack(this);

        protected override void OnStart()
        {
            base.OnStart();

            if (!_inGameManager)
            {
                _inGameManager = StaticServiceLocator.Instance.Get<InGameManager>();
            }
            
            if (!_inGameManager.PlayerDataDic.TryGet(PlayerRef.FromEncoded(Context.SourcePlayer), out var playerData))
            {
                Debug.LogError("PlayerDataが見つかりません。通常攻撃を実行できません。");
                return;
            }
            
            var hitCount = Physics.OverlapSphereNonAlloc(
                playerData.gameObject.transform.position, 
                _attackRadius,
                _hitBuffer,
                _hitMask
            );

            for (var i = 0; i < hitCount; i++)
            {
                var hit = _hitBuffer[i];
                var target = hit.GetComponent<PlayerHealth>();
                if (!target || target.HasInputAuthority)
                    continue;

                var hitData = new HitData
                {
                    HitActionType = HitActionType.Damage,
                    Amount = _attackDamage,
                    ExecutorRef = PlayerRef.FromEncoded(Context.SourcePlayer),  //TODO: Photonで依存するクラス以外はPlayerRefをIntで持つように変更したい
                    TargetRef = target.Object.InputAuthority,
                    Target = target,
                };

                target.TakeHit(ref hitData);
            }
        }
    }
}
