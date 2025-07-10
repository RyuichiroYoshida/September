using System;
using System.Linq;
using Fusion;
using InGame.Health;
using InGame.Player;
using September.Common;
using UnityEngine;

namespace InGame.Exhibit
{
    public class CannonBall : NetworkBehaviour
    {
        [Header("参照")]
        [SerializeField] private GameObject _model;
        [SerializeField] private Rigidbody _rigidbody;
        [Header("設定")]
        [SerializeField] private float _power = 10f;
        [SerializeField] private float _upwardForce = 5f;
        [SerializeField] private int _damageAmount = 10;
        
        private Transform _currentOwnerTransform;
        private int _equippedInteractor;
        private Vector3 _startPosition;
        public event Action OnCannonBallHit;

        private void Start()
        {
            _rigidbody.isKinematic = true;
            _startPosition = transform.position;
        }

        [Rpc(RpcSources.All, RpcTargets.All)]
        public void Rpc_EquipToHand(int interactor)
        {
            var playerRef = PlayerRef.FromEncoded(interactor);
            if (Runner.TryGetPlayerObject(playerRef, out var playerObj))
            {
                var hand = playerObj.GetComponentInChildren<HandSocket>()?.Sockets.FirstOrDefault();
                if (hand)
                {
                    _model.transform.SetParent(hand);
                    _model.transform.localPosition = Vector3.zero;
                    _model.transform.localRotation = Quaternion.identity;
                    _model.transform.localScale = Vector3.one;
                    _currentOwnerTransform = playerObj.transform;
                    _equippedInteractor = interactor;
                }
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!GetInput(out PlayerInput input)) return;

            if (input.Buttons.IsSet(PlayerButtons.Attack))
            {
                transform.position = _model.transform.position;
                _rigidbody.isKinematic = false;

                // 斜め上方向に投げるベクトルを作成
                var forward = _currentOwnerTransform.forward;
                var upward = _currentOwnerTransform.up;
                var throwDir = (forward + upward * _upwardForce).normalized;

                _rigidbody.AddForce(throwDir * _power, ForceMode.Impulse);

                Rpc_UnequipFromHand();
            }
        }

        [Rpc(RpcSources.All, RpcTargets.All)]
        public void Rpc_UnequipFromHand()
        {
            if (_model.transform.parent)
            {
                _model.transform.SetParent(this.gameObject.transform);
            }
        }

        private void OnCollisionEnter(Collision other)
        {
            if (other.gameObject.TryGetComponent<IDamageable>(out var damageable))
            {
                if (damageable.OwnerPlayerRef == PlayerRef.FromEncoded(_equippedInteractor))
                {
                    // 自分自身にダメージを与えない
                    return;
                }
                var hitData = new HitData(HitActionType.Damage, _damageAmount, PlayerRef.FromEncoded(_equippedInteractor), damageable.OwnerPlayerRef);
                damageable.TakeHit(ref hitData); 
            }
            
            // 衝突後、カノンボールを元の位置に戻す
            _rigidbody.isKinematic = true;
            transform.position = _startPosition;
            transform.rotation = Quaternion.identity;
            OnCannonBallHit?.Invoke();
        }
    }
}
