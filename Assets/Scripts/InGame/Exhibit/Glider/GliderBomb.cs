using DG.Tweening;
using Fusion;
using InGame.Health;
using InGame.Player;
using September.Common;
using September.InGame.Effect;
using UnityEngine;

namespace September
{
    public class GliderBomb : NetworkBehaviour
    {
        [SerializeField] private EffectType _effectType;
        [SerializeField] private LayerMask _layerMask;
        [SerializeField] float _knockBackPower = 10f;
        [SerializeField] float _knockBackUpwardPower = 0.5f;
        [SerializeField] float _knockBackDuration = 0.5f;
        private EffectSpawner _effectSpawner;
        
        // アニメーション用
        private Sequence _sequence;
        private Renderer _renderer;
        private Vector3 _defaultScale;
        [Networked] private TickTimer ExplodeTimer { get; set; }
        [Networked] private int Damage { get; set; } = 10;
        [Networked] private float ExplodeSeconds { get; set; } = 3f;
        [Networked] private float ExplodeRadius { get; set; } = 3f;
        [Networked] private PlayerRef PlayerRef { get; set; }

        public override void Spawned()
        {
            _effectSpawner = StaticServiceLocator.Instance.Get<EffectSpawner>();
            _renderer = GetComponent<Renderer>();
            _defaultScale = transform.localScale;
            StartPulse();
        }

        public override void FixedUpdateNetwork()
        {
            base.FixedUpdateNetwork();
            if(ExplodeTimer.Expired(Runner))
            {
                Bump();
                _sequence.Kill();
                Runner.Despawn(Object);
            }
        }

        public void Initialize(float seconds, float radius, int damage, PlayerRef ownerPlayerRef)
        {
            ExplodeSeconds = seconds;
            ExplodeRadius = radius;
            Damage = damage;
            PlayerRef = ownerPlayerRef;
            
            ExplodeTimer = TickTimer.CreateFromSeconds(Runner, ExplodeSeconds);
        } 
        
        private void StartPulse()
        {
            _sequence = DOTween.Sequence();

            _sequence.AppendInterval(1);

            _sequence.Append(
                transform.DOScale(
                        _defaultScale * 1.2f,
                        0.15f
                    )
                    .SetEase(Ease.OutQuad)
            );

            _sequence.Join(
                _renderer.material
                    .DOColor(Color.red, 0.15f)
            );

            _sequence.Append(
                transform.DOScale(
                        _defaultScale,
                        0.2f
                    )
                    .SetEase(Ease.InQuad)
            );

            _sequence.Join(
                _renderer.material
                    .DOColor(Color.gray, 0.2f)
            );

            _sequence.SetLoops(-1);
        }

        private void Bump()
        {
            StateAuthorityHit(transform.position, transform.up,  PlayerRef);
            _effectSpawner.RequestPlayOneShotEffect(_effectType, transform.position, transform.rotation);
        }
        
        // TODO:似たような攻撃処理がとても多そう？
        public void StateAuthorityHit(Vector3 position, Vector3 normal, PlayerRef usePlayer)
        {
            if(!HasStateAuthority) return;
            var colliders = Physics.OverlapSphere(position, ExplodeRadius, _layerMask);
            // ダメージ処理
            foreach (var col in colliders)
            {
                var damageable = col.GetComponentInParent<IDamageable>();
                if (damageable == null) continue;
                if (damageable.OwnerPlayerRef == usePlayer) continue;
                TakeDamage(damageable, usePlayer);
                KnockBack(col.gameObject, col.transform.position - position);
            }
        }

        private void TakeDamage(IDamageable damageable, PlayerRef usingPlayer)
        {
            var hitData = new HitData(HitActionType.RangedDamage, Damage, usingPlayer,
                damageable.OwnerPlayerRef);
			
            damageable.TakeHit(ref hitData);
        }

        private void KnockBack(GameObject obj, Vector3 direction)
        {
            var playerMovement = obj.transform.GetComponentInParent<PlayerMovement>();
            if (playerMovement)
            {
                direction.y = 0;
                direction.Normalize();
                playerMovement.KnockBack(direction * _knockBackPower + Vector3.up * _knockBackUpwardPower,
                    _knockBackDuration);
            }
        }
    }
}
