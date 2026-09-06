using Fusion;
using Ingame.Tanihira;
using InGame.Health;
using September.Common;
using September.InGame.Effect;
using UnityEngine;

namespace InGame.Exhibit
{
    public class MuramasaInteractInvoker : NetworkBehaviour
    {
        [SerializeField] private Vector3 _muramasaOffset = new Vector3(0, 0.5f, 0);
        [SerializeField] private int _attackDamage = 1;
        [SerializeField] private float _attackInterval = 0.2f;
        [SerializeField] private float _attackRadius = 2.25f;
        [SerializeField] private float _muramasaDuration = 10f;
        [Networked] public TickTimer AttackTimer { get; set; }
        [Networked] public TickTimer DurationTimer { get; set; }
        private EffectSpawner EffectSpawner => StaticServiceLocator.Instance.Get<EffectSpawner>();
        private Transform _currentParent;
        private PlayerRef _currentOwner;

        private EffectID _currentEffectId;

        public override void FixedUpdateNetwork()
        {
            if (_currentOwner.IsNone) return;

            if (AttackTimer.ExpiredOrNotRunning(Runner) && HasStateAuthority)
            {
                var hits = Physics.OverlapSphere(_currentParent.transform.position + _muramasaOffset, _attackRadius);
                //タニヒラ用にformationManagerを取得
                PlayerDatabase.Instance.PlayerObjectDic.TryGet(_currentOwner, out var player);
                FormationManager formationManager = player.GetComponent<FormationManager>();

                foreach (var hit in hits)
                {
                    //隊列がいる場合ははじく
                    if (formationManager)
                    {
                        var friendBase = hit.GetComponent<FriendBase>();
                        if (friendBase != null && formationManager.FriendsList.Contains(friendBase))
                            continue;
                    }

                    var root = hit.transform.root;
                    if (root.TryGetComponent<IDamageable>(out var damageable) &&
                        damageable.OwnerPlayerRef != _currentOwner)
                    {
                        var hitData = new HitData(HitActionType.Damage, _attackDamage,
                            _currentOwner, damageable.OwnerPlayerRef);
                        damageable.TakeHit(ref hitData);
                    }
                }
                AttackTimer = TickTimer.CreateFromSeconds(Runner, _attackInterval);
            }

            if (DurationTimer.ExpiredOrNotRunning(Runner) && HasStateAuthority)
            {
                StopAttack();
            }
        }
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void Rpc_StartAttack(int interactor)
        {
            if (!_currentOwner.IsNone) return;

            var playerRef = PlayerRef.FromEncoded(interactor);
            if (!PlayerDatabase.Instance.PlayerObjectDic.TryGet(playerRef, out var playerObj)) return;
            _currentEffectId = EffectSpawner.RequestPlayLoopEffect(
                EffectType.Muramasa,
                playerObj.transform.position + _muramasaOffset,
                Quaternion.identity,
                playerObj.transform
            );
            _currentParent = playerObj.transform;
            _currentOwner = playerRef;
            DurationTimer = TickTimer.CreateFromSeconds(Runner, _muramasaDuration);
        }

        private void StopAttack()
        {
            EffectSpawner.StopEffect(_currentEffectId);
            _currentOwner = PlayerRef.None;
            _currentParent = null;
        }
#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (_currentParent == null)
            {
                Gizmos.DrawWireSphere(transform.position + _muramasaOffset, _attackRadius);
            }
            else
            {
                Gizmos.DrawWireSphere(_currentParent.position + _muramasaOffset, _attackRadius);
            }
        }
#endif
    }
}
