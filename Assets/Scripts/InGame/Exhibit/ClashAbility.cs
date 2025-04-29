using Fusion;
using UnityEngine;

namespace September.InGame
{
    public class ClashAbility : BaseAbility
    {
        [SerializeField] LayerMask _playerLayerMask;
        [SerializeField] float _meleeRadius = 0.75f;
        [SerializeField] Vector3 _meleeOffset;
        [SerializeField] float _throwPower = 10f;
        [SerializeField] Vector3 _throwOffset;
        [SerializeField] Projectile _projectilePrefab;
        [SerializeField] GameObject _cannonBallView;
        [SerializeField] GameObject _hammerView;
        Collider[] _colliders = new Collider[10];
        Vector3 _overlapPosition;
        [Networked, OnChangedRender(nameof(ChangeHandItem)), HideInInspector] 
        public NetworkBool HasCannonBall { get; set; }
        [Networked] TickTimer AttackTimer { get; set; }
        public override AbilityType GetAbilityType() => AbilityType.Clash;
        public override void Spawned()
        {
            base.Spawned();
            ChangeHandItem();
        }

        public override void Attack()
        {
            if (!AttackTimer.ExpiredOrNotRunning(Runner)) return;
            if (HasCannonBall)
            {
                ThrowAttack();
            }
            else
            {
                MeleeAttack();
            }
            AttackTimer = TickTimer.CreateFromSeconds(Runner, PlayerData.AttackInterval);
        }

        void MeleeAttack()
        {
            Animator.SetTrigger("Attack");
            _overlapPosition = transform.position + transform.TransformVector(_meleeOffset);
            var count = Runner.GetPhysicsScene()
                .OverlapSphere(_overlapPosition, _meleeRadius, _colliders, _playerLayerMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (_colliders[i].TryGetComponent(out PlayerController playerStatus) 
                    && playerStatus.Object.Id != Object.Id)
                {
                    playerStatus.TakeDamage(PlayerController, PlayerData.AttackDamage);
                    return;
                }
            }
        }

        void ThrowAttack()
        {
            var projectile = Runner.Spawn(_projectilePrefab, transform.position + transform.TransformVector(_throwOffset));
            projectile.NetworkRb.Rigidbody.linearVelocity = transform.forward * _throwPower;
            projectile.Owner = PlayerController;
            HasCannonBall = false;
        }
        /// <summary>
        /// 手に持っているアイテムを切り替える
        /// </summary>
        void ChangeHandItem()
        {
            if (HasCannonBall)
            {
                _cannonBallView.SetActive(true);
                _hammerView.SetActive(false);
            }
            else
            {
                _cannonBallView.SetActive(false);
                _hammerView.SetActive(true);
            }
        }

        public override void Use()
        {
        }

        public override void InteractWith(ExhibitBase exhibit)
        {
            exhibit.Interact(this);
        }
    }
}