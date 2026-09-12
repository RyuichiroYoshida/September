using System.Collections.Generic;
using Fusion;
using InGame.Common;
using InGame.Health;
using September.Common;
using UnityEngine;
using UnityEngine.Splines;

namespace InGame.Player.Okubo
{
    public class AbilityHookAttack : NetworkBehaviour
    {
        [SerializeField] private AnimationClipPlayer _animationClipPlayer;
        [SerializeField] private AnimationClip _shotClip;
        [SerializeField] private AnimationClip _aimClip;
        [SerializeField] private AnimationClip _pullClip;
        [SerializeField] private PlayerMovement _playerMovement;
        [SerializeField] private PlayerInputManager _playerInputManager;
        [SerializeField] private Transform _wireOrigin;
        [SerializeField] private SplineContainer _wireSpline;
        [SerializeField] private GameObject _wireMesh;
        [SerializeField] private Transform _hookMesh;
        [SerializeField] private float _stretchDuration;
        [SerializeField] private float _pullDuration;
        [SerializeField] private float _wireLength;
        [SerializeField] private float _stretchedWaitTime = 0.3f;
        [SerializeField] private float _wireThickness;
        [SerializeField] private float _hitRadius;
        [SerializeField] private Vector3 _hookOffsetRotation;
        [SerializeField] private int _damageAmount;
        [SerializeField] private float _resistanceAmount;
        [SerializeField] private float _missAttackCoolTime;
        [SerializeField] private float _hitCoolTime;

        private HookAttackState _currentState;
        private float _currentHookLength;
        private float _startAttackTime;
        private bool _isPlayAimClip;
        private float _waitTimer;
        /// <summary>PlayerMovementなどのキャッシュ用 </summary>
        private Dictionary<PlayerRef, HookTargetData> _targetData = new();
        private PlayerRef _ownerRef;

        public override void Spawned()
        {
            _wireMesh.gameObject.SetActive(false);
            _ownerRef = Object.InputAuthority;
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;

            _playerInputManager.GetPlayerInput(out var input);

            switch (_currentState)
            {
                case HookAttackState.Idol:
                    //フック攻撃開始
                    if (input.Buttons.IsSet(PlayerButtons.Ability1))
                        ChangeState(HookAttackState.Stretching);
                    break;
                case HookAttackState.Stretching:
                    OnStretching();
                    break;
                case HookAttackState.Pulling:
                    OnPulling();
                    break;
                case HookAttackState.Stretched:
                case HookAttackState.CoolDown:
                    //待機処理
                    OnWait();
                    break;
            }
        }

        private void ChangeState(HookAttackState state)
        {
            _currentState = state;

            switch (state)
            {
                case HookAttackState.Idol:
                    break;
                //フック攻撃初期化
                case HookAttackState.Stretching:
                    RPC_ChangeWireActive(true);
                    _targetData.Clear();
                    _playerMovement.IsHookLocked = true;
                    _currentHookLength = 0;
                    _startAttackTime = Runner.SimulationTime;
                    _isPlayAimClip = false;
                    _animationClipPlayer.PlayClip(_shotClip);
                    break;
                case HookAttackState.Stretched:
                    _waitTimer = _stretchedWaitTime;
                    break;
                case HookAttackState.Pulling:
                    if (_isPlayAimClip)
                    {
                        _animationClipPlayer.StopClip(_aimClip);
                        _isPlayAimClip = false;
                    }
                    _animationClipPlayer.PlayClip(_pullClip);
                    break;
                case HookAttackState.CoolDown:
                    bool isTarget = false;
                    foreach (var kv in _targetData)
                    {
                        if (!kv.Value.IsTarget) continue;
                        RPC_HookEnd(kv.Key);
                        kv.Value.IsTarget = false;
                        isTarget = true;
                    }
                    RPC_ChangeWireActive(false);
                    _waitTimer = isTarget ? _hitCoolTime : _missAttackCoolTime;
                    _playerMovement.IsHookLocked = false;
                    break;
            }
        }

        /// <summary>
        /// 伸ばす
        /// </summary>
        private void OnStretching()
        {
            _currentHookLength += _wireLength / _stretchDuration * Runner.DeltaTime;

            //最大の長さまで伸びた
            if (_currentHookLength >= _wireLength)
            {
                _currentHookLength = _wireLength;
                ChangeState(HookAttackState.Stretched);
            }
            if(!_isPlayAimClip && Runner.SimulationTime - _startAttackTime > _shotClip.length)
            {
                _animationClipPlayer.PlayClipLoop(_aimClip);
                _isPlayAimClip = true;
            }

            UpdateHookLength(_currentHookLength, this.transform.forward);
            GetHitPlayer(_currentHookLength, this.transform.forward);
        }

        private void OnPulling()
        {
            float maxResistance = 0;
            foreach (var player in _targetData.Values)
            {
                Vector3 inputDirection = new Vector3(player.PlayerMovement.MoveDirection.x, 0, player.PlayerMovement.MoveDirection.y);
                var angle = Vector3.Angle(transform.forward, inputDirection);

                float resistance = 1f - (angle / 180);
                maxResistance = Mathf.Max(resistance * inputDirection.magnitude, maxResistance);
                Debug.Log($"抵抗 {player.Player}  {resistance}");
            }
            _currentHookLength -= (_wireLength / _pullDuration - maxResistance * _resistanceAmount) * Runner.DeltaTime;

            if (_currentHookLength <= 0)
            {
                _currentHookLength = 0;
                ChangeState(HookAttackState.CoolDown);
            }

            UpdateHookLength(_currentHookLength, transform.forward);

            var hookSqr = (this.transform.position - _hookMesh.transform.position).sqrMagnitude;
            foreach (var kv in _targetData)
            {
                if (kv.Value.IsHookFollow) continue;
                if (!PlayerDatabase.Instance.PlayerObjectDic.TryGet(kv.Key, out var obj)) continue;
                var targetSqr = (this.transform.position - obj.transform.position).sqrMagnitude;

                if (targetSqr > hookSqr)
                {
                    HookFollow(kv.Key);
                    _targetData[kv.Key].IsHookFollow = true;
                }
            }

        }

        private void OnWait()
        {
            _waitTimer -= Runner.DeltaTime;

            if (_waitTimer > 0)
                return;

            switch (_currentState)
            {
                case HookAttackState.Stretched:
                    ChangeState(HookAttackState.Pulling);
                    break;

                case HookAttackState.CoolDown:
                    ChangeState(HookAttackState.Idol);
                    break;
            }
        }

        private void UpdateHookLength(float length, Vector3 direction)
        {
            var startPosition = _wireOrigin.transform.position;
            var endPosition = startPosition + direction * length;

            var spline = _wireSpline.Spline;

            // ワールド座標 → Splineのローカル座標
            var localStartPosition = _wireSpline.transform.InverseTransformPoint(startPosition);
            var localEndPosition = _wireSpline.transform.InverseTransformPoint(endPosition);

            // SplineのPosition設定
            var start = spline[0];
            start.Position = localStartPosition;
            spline[0] = start;

            var end = spline[spline.Count - 1];
            end.Position = localEndPosition;
            spline[spline.Count - 1] = end;

            //フックの位置を変える
            _hookMesh.position = endPosition;

            // フックの向きをワイヤー方向に合わせる
            if (direction.sqrMagnitude > 0.0001f)
            {
                _hookMesh.rotation = Quaternion.LookRotation(direction.normalized) * Quaternion.Euler(_hookOffsetRotation);
            }
        }

        private void GetHitPlayer(float length, Vector3 direction)
        {
            Vector3 position = _wireOrigin.transform.position + direction * length;
            var hitObjects = Physics.OverlapSphere(position, _hitRadius);

            foreach (var obj in hitObjects)
            {
                GameObject hitObject = obj.transform.root.gameObject;
                if (hitObject == this.gameObject || !hitObject.CompareTag("Player")) continue;

                //ヒットしたオブジェクトからPlayerRefを取得
                foreach (var pair in PlayerDatabase.Instance.PlayerObjectDic)
                {
                    if (pair.Value.gameObject == hitObject)
                    {
                        if (_targetData.ContainsKey(pair.Key)) continue;

                        //ターゲットデータに入れる
                        var target = GetOrCreateTargetData(pair.Key);
                        if (target == null) continue;
                        //フック攻撃対象にする
                        target.IsTarget = true;

                        RPC_HookStart(pair.Key);

                        //ダメージ処理
                        if (pair.Value.TryGetComponent(out IDamageable damageable))
                        {
                            var hitData = new HitData(HitActionType.Damage, _damageAmount, _ownerRef, damageable.OwnerPlayerRef);
                            damageable.TakeHit(ref hitData);
                        }
                        break;
                    }
                }
            }
        }

        private void HookFollow(PlayerRef playerRef)
        {
            Debug.Log($"follow {Object.InputAuthority} => {playerRef}");
            var targetData = GetOrCreateTargetData(playerRef);
            if (targetData == null || !targetData.PlayerObject.HasStateAuthority) return;

            targetData.PlayerMovement.OnHookFollow(_hookMesh.transform);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_HookStart(PlayerRef playerRef)
        {
            Debug.Log($"start {Object.InputAuthority} => {playerRef}");
            var targetData = GetOrCreateTargetData(playerRef);
            if (targetData == null || !targetData.PlayerObject.HasStateAuthority) return;

            Debug.Log("StartHook");
            targetData.PlayerMovement.OnStartHook();
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_HookEnd(PlayerRef playerRef)
        {
            Debug.Log($"end {Object.InputAuthority} => {playerRef}");
            var targetData = GetOrCreateTargetData(playerRef);
            if (targetData == null || !targetData.PlayerObject.HasStateAuthority) return;

            targetData.PlayerMovement.OnEndHook();
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_ChangeWireActive(bool active)
        {
            Debug.Log("ワイヤーアクティブ " + active);
            _wireMesh.gameObject.SetActive(active);
        }

        /// <summary>
        /// RPCで飛んだ先にTargetDataがない時の対策
        /// </summary>
        private HookTargetData GetOrCreateTargetData(PlayerRef playerRef)
        {
            if (_targetData.TryGetValue(playerRef, out var targetData)) return targetData;

            if (!CreateTargetData(playerRef)) return null;

            return _targetData[playerRef];
        }

        private bool CreateTargetData(PlayerRef playerRef)
        {
            if (!PlayerDatabase.Instance.PlayerObjectDic.TryGet(playerRef, out var playerObject)) return false;

            //ターゲットに入れる
            _targetData.Add(playerRef, new HookTargetData(playerRef, playerObject));
            return true;
        }

        private enum HookAttackState
        {
            Idol, Stretching, Stretched, Pulling, CoolDown
        }

        private class HookTargetData
        {
            public PlayerRef Player { get; private set; }
            public NetworkObject PlayerObject { get; private set; }
            private PlayerMovement _playerMovement;
            public PlayerMovement PlayerMovement
            {
                get
                {
                    if (_playerMovement == null)
                    {
                        _playerMovement = PlayerObject.GetComponentInChildren<PlayerMovement>();
                    }

                    return _playerMovement;
                }
            }
            public bool IsHookFollow;
            public bool IsTarget;

            public HookTargetData(PlayerRef playerRef, NetworkObject playerObject)
            {
                Player = playerRef;
                PlayerObject = playerObject;
                IsHookFollow = false;
                IsTarget = false;
            }
        }
    }
}