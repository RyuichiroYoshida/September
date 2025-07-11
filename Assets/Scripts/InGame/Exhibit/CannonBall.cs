using System;
using System.Collections.Generic;
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
        [SerializeField] private NetworkObject _networkObject;
        [Header("設定")]
        [SerializeField] private float _power = 10f;
        [SerializeField] private float _upwardForce = 5f;
        [SerializeField] private int _damageAmount = 10;
        [SerializeField] private Vector3 _startPosition;
        
        private Transform _currentOwnerTransform;
        private int _equippedInteractor;
        [SerializeField] private bool _isLaunching;
        private MeleeHitboxExecutor _meleeHitboxExecutor;

        public event Action OnCannonBallHit;

        private void Start()
        {
            _rigidbody.isKinematic = true;
            _meleeHitboxExecutor = new MeleeHitboxExecutor(new List<Transform>() { _model.transform }, 
                hitboxRadius: _model.transform.localScale.x * 0.5f);
            
            _meleeHitboxExecutor.OnHit += hit =>
            {
                var didHitSomething = false;
                if (hit.gameObject == _model) return; // 自分自身には当たらないように
                if (hit.TryGetComponent<IDamageable>(out var damageable))
                {
                    if (damageable.OwnerPlayerRef != PlayerRef.FromEncoded(_equippedInteractor))
                    {
                        var hitData = new HitData(HitActionType.Damage, _damageAmount, PlayerRef.FromEncoded(_equippedInteractor), damageable.OwnerPlayerRef);
                        damageable.TakeHit(ref hitData);
                        didHitSomething = true;
                    }
                }
                else
                {
                    
                    // IDamageable ではないが何かに当たった場合もヒット扱い
                    didHitSomething = true;
                }

                if (didHitSomething)
                {
                    Debug.Log( $"{hit.gameObject.name} にヒット");
                    Rpc_ResetCannonBall(); // 全体にリセット
                    OnCannonBallHit?.Invoke();
                }
            };
        }

        [Rpc(RpcSources.All, RpcTargets.All)]
        public void Rpc_EquipToHand(int interactor)
        {
            var playerRef = PlayerRef.FromEncoded(interactor);
            _networkObject.AssignInputAuthority(playerRef);
            if (Runner.TryGetPlayerObject(playerRef, out var playerObj))
            {
                var hand = playerObj.GetComponentInChildren<HandSocket>()?.Sockets.FirstOrDefault();
                if (hand)
                {
                    _model.transform.SetParent(hand);
                    _model.transform.localPosition = Vector3.zero;
                    _model.transform.localRotation = Quaternion.identity;
                    _currentOwnerTransform = playerObj.transform;
                    _equippedInteractor = interactor;
                }
            }
        }

        private void Update()
        {
            //Debug.Log($"is Kinematic{_rigidbody.isKinematic} 速度{_rigidbody.linearVelocity}");
            if (Runner?.IsServer == false) return;
            
           if (_isLaunching) _meleeHitboxExecutor.Tick(Time.deltaTime);
        }

        public override void FixedUpdateNetwork()
        {
            if (!GetInput(out PlayerInput input) || _isLaunching) return;

            if (input.Buttons.IsSet(PlayerButtons.Attack))
            {
                transform.position = _model.transform.position;
                _rigidbody.isKinematic = false;

                // 斜め上方向に投げるベクトルを作成
                var forward = _currentOwnerTransform.forward;
                var upward = _currentOwnerTransform.up;
                var throwDir = (forward + upward * _upwardForce).normalized;
                _rigidbody.AddForce(throwDir * _power, ForceMode.Impulse);

                Rpc_Launch();
                
            }
        }

        [Rpc(RpcSources.All, RpcTargets.All)]
        private void Rpc_Launch()
        {
            _model.transform.SetParent(null, true);
            _isLaunching = true;
        }

        public override void Render()
        {
            if (_isLaunching)
            {
                _model.transform.position = transform.position;
            }
        }

        [Rpc(RpcSources.All, RpcTargets.All)]
        private void Rpc_ResetCannonBall()
        {
            _rigidbody.isKinematic = true;
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;

            Debug.Log($"{transform.position} から {_startPosition} にリセット");
            transform.position = _startPosition;
            transform.rotation = Quaternion.identity;

            _model.transform.SetParent(transform); // 先に SetParent
            _model.transform.localPosition = Vector3.zero;
            _model.transform.localRotation = Quaternion.identity;

            _isLaunching = false;
            _networkObject.RemoveInputAuthority();
        }
    }
}
