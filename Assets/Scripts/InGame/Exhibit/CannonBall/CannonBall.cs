using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using Fusion.Addons.Physics;
using InGame.Health;
using InGame.Interact;
using InGame.Player;
using September.Common;
using September.InGame.Effect;
using UniRx;
using UnityEngine;

namespace InGame.Exhibit
{
    /// <summary>
    /// 大砲の砲丸を表現するクラス
    /// </summary>
    public class CannonBall : NetworkBehaviour
    {
        private enum CannonBallState
        {
            Idle,
            Equipped,
            Launched,
            Resetting
        }

        [Header("参照")]
        [SerializeField] private GameObject _model;
        [SerializeField] private Rigidbody _rigidbody;
        [SerializeField] private NetworkObject _networkObject;
        [SerializeField] private NetworkRigidbody3D _networkRigidbody3D;
        [SerializeField] private InteractableBase _interactableBase;
        [SerializeField] private Collider _modelCollider;
        [SerializeField] private TrajectoryVisualizer _trajectoryVisualizer;

        [Header("設定")]
        [SerializeField] private float _power = 10f;
        [SerializeField] private float _upwardForce = 5f;
        [SerializeField] private int _damageAmount = 10;
        [SerializeField] private Vector3 _startPosition;
        [SerializeField] private float _maxFlightTime = 3f;
        [SerializeField] private float _explosionRadius = 5f;

        [Header("デバッグ")]
        [SerializeField] private float _launchElapsedTime;
        [SerializeField] private bool _isAiming;
        [SerializeField] private CannonBallState _cannonBallState = CannonBallState.Idle;
        private MeleeHitboxExecutor _flyingHitBoxExecutor;
        private MeleeHitboxExecutor _explodeHitBoxExecutor;
        private Transform _currentOwnerTransform;     // 投げ方向用
        private Transform _followTargetTransform;     // 見た目追従用
        private int _equippedInteractor;
        private bool _shouldFollow;
        [Networked] private CannonBallState State { get; set; }
        private EffectSpawner EffectSpawner => StaticServiceLocator.Instance.Get<EffectSpawner>();
        public event Action OnCannonBallHit;
        
        public override void Spawned() => InitializeCannonBall();
        private void Update()
        {
            CheckHit();
            if (Object && Object.IsValid)
            {
                _cannonBallState = State;
            }
        }

        public override void FixedUpdateNetwork()
        {
            GetFireInput();
            // if (GetInput(out PlayerInput input) && input.Buttons.IsSet(PlayerButtons.Aim))
            // {
            //     _isAiming = true;
            //     //FOVを上げる
            //     
            // }
            // else
            // {
            //     _isAiming = false;
            // }
        }

        private void LateUpdate()
        {
            ShowTrajectory();
            TrackModelToFollowTarget();
            if (Object && Object.IsValid) _model.SetActive(State != CannonBallState.Resetting);
        }

        public void EquipToInteractor(int interactor)
        {
            var playerRef = PlayerRef.FromEncoded(interactor);
            _networkObject.AssignInputAuthority(playerRef);
            State = CannonBallState.Equipped;
            Rpc_SetModelAdulationTarget(interactor);
        }

        /// <summary>
        /// 空中での固定、当たり判定クラスの生成、クールダウン開始＆終了時にStateを更新する処理の登録、何かに当たった時の処理の登録
        /// を行う。
        /// </summary>
        private void InitializeCannonBall()
        {
            _networkRigidbody3D.RBIsKinematic = true;

            Observable.EveryUpdate()
                .Select(_ => _interactableBase.IsInCooldown())
                .DistinctUntilChanged()
                .Subscribe(inCoolDown =>
                {
                    State = inCoolDown ? CannonBallState.Resetting : CannonBallState.Idle;
                }).AddTo(this);

            _flyingHitBoxExecutor = new MeleeHitboxExecutor(new List<Transform>() { _model.transform }, hitboxRadius: _model.transform.localScale.x * 0.5f);
            //_flyingHitBoxExecutor.OnHit += OnHitSomething;
            _explodeHitBoxExecutor = new MeleeHitboxExecutor(new List<Transform>() { transform }, hitboxRadius: _explosionRadius);
            //_explodeHitBoxExecutor.OnHit += OnExplodeHit;
            
            State = CannonBallState.Idle;
        }

        private void OnHitSomething(Collider hit)
        {
            //とんでいないとき、弾の見た目のコリジョンに当たった時、投げた人自身にあたったときはそのまま
            if (State != CannonBallState.Launched || State == CannonBallState.Resetting || hit.gameObject == _model ||
                hit.gameObject.GetComponentInParent<NetworkObject>()?.InputAuthority ==
                _networkObject.InputAuthority) return;

            
            EffectSpawner.RequestPlayOneShotEffect(EffectType.Explosion, transform.position, Quaternion.identity);
            _explodeHitBoxExecutor.ExecuteHitCheck();
            ResetCannonBall();
            OnCannonBallHit?.Invoke();
        }
        
        private void OnExplodeHit(Collider hit)
        {
            //とんでいないとき、弾の見た目のコリジョンに当たった時、投げた人自身にあたったときはそのまま
            if (State != CannonBallState.Launched || State == CannonBallState.Resetting || hit.gameObject == _model ||
                hit.gameObject.GetComponentInParent<NetworkObject>()?.InputAuthority ==
                _networkObject.InputAuthority) return;

            var root = hit.transform.root;
            if (root.TryGetComponent<IDamageable>(out var damageable) &&
                damageable.OwnerPlayerRef != PlayerRef.FromEncoded(_equippedInteractor))
            {
                Debug.Log($"[CannonBall] ダメージを与えます: {damageable.OwnerPlayerRef}");
                var hitData = new HitData(HitActionType.RangedDamage, _damageAmount,
                    PlayerRef.FromEncoded(_equippedInteractor), damageable.OwnerPlayerRef);
                damageable.TakeHit(ref hitData);
            }
        }

        /// <summary>
        /// モデルの追従先を設定するRPC。-1で見た目用オブジェクトが大砲の弾事態に追従する
        /// それ以外の時は、指定されたインタラクターの手に追従する。
        /// </summary>
        /// <param name="interactor"></param>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void Rpc_SetModelAdulationTarget(int interactor = -1)
        {
            if (interactor == -1)
            {
                _modelCollider.enabled = true;
                _model.transform.position = transform.position;
                _model.transform.rotation = transform.rotation;
                _currentOwnerTransform = null;
                _followTargetTransform = null;
                _equippedInteractor = -1;
            }
            else
            {
                var playerRef = PlayerRef.FromEncoded(interactor);
                if (!Runner.TryGetPlayerObject(playerRef, out var playerObj)) return;
                var hand = playerObj.GetComponentInChildren<HandSocket>()?.Sockets.FirstOrDefault();
                if (!hand) return;
                _modelCollider.enabled = false;
                _currentOwnerTransform = playerObj.transform;
                _followTargetTransform = hand;
                _equippedInteractor = interactor;
                _shouldFollow = true;
            }
        }

        /// <summary>
        /// ホストでとんでいるときだけ当たり判定を行う
        /// </summary>
        private void CheckHit()
        {
            if (!HasStateAuthority || State != CannonBallState.Launched) return;
            _flyingHitBoxExecutor.Tick(Time.deltaTime);

            _launchElapsedTime += Time.deltaTime;
            if (_launchElapsedTime < _maxFlightTime) return;    // 飛行時間が最大を超えたらリセット
            ResetCannonBall();
            OnCannonBallHit?.Invoke();
        }

        private void GetFireInput()
        {
            //入力がない、または攻撃ボタンが押されていない、またはすでに発射済みなら何もしない
            if (!GetInput(out PlayerInput input) || !input.Buttons.IsSet(PlayerButtons.Attack) || State == CannonBallState.Launched) return;
            if (!_currentOwnerTransform)
            {
                Debug.LogWarning("[CannonBall] 現在の所有者のTransformが設定されていません。");
                return;
            }

            // 向きを先に計算
            var forward = _currentOwnerTransform.forward;
            var upward = _currentOwnerTransform.up;
            var throwDir = (forward + upward * _upwardForce).normalized;
            _networkRigidbody3D.Teleport(_model.transform.position);
            _networkRigidbody3D.RBIsKinematic = false;
            _rigidbody.AddForce(throwDir * _power, ForceMode.Impulse);

            Rpc_SetModelAdulationTarget();
            State = CannonBallState.Launched;
        }

        private void ResetCannonBall()
        {
            _networkRigidbody3D.RBIsKinematic = true;
            _networkRigidbody3D.Teleport(_startPosition);

            _flyingHitBoxExecutor.Init();
            State = CannonBallState.Resetting;
            if (_interactableBase.LastInteractTime < 0f)
            {   // クールダウン時間が設定されていない場合は、Observableを使わずに直接StateをIdleにする
                State = CannonBallState.Idle;
            }
            _launchElapsedTime = 0f;
            _networkObject.RemoveInputAuthority();
            Rpc_SetModelAdulationTarget();
        }

        private void ShowTrajectory()
        {
            if (_isAiming && _currentOwnerTransform)
            {
                var startPos = _model.transform.position;
                var forward = _currentOwnerTransform.forward;
                var upward = _currentOwnerTransform.up;
                var velocity = (forward + upward * _upwardForce).normalized * _power;

                _trajectoryVisualizer?.ShowTrajectory(startPos, velocity);
            }
            else
            {
                _trajectoryVisualizer?.Hide();
            }
        }

        private void TrackModelToFollowTarget()
        {
            if (_shouldFollow && _followTargetTransform)
            {
                _model.transform.position = _followTargetTransform.position;
                _model.transform.rotation = _followTargetTransform.rotation;
            }
        }
    }
}
