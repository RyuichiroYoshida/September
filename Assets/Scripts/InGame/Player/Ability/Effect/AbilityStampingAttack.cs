using System.Collections.Generic;
using Fusion;
using InGame.Common;
using InGame.Health;
using InGame.Player;
using InGame.Player.Ability;
using September.Common;
using UnityEngine;

namespace InGame.Exhibit
{
    public class AbilityStampingAttack : AbilityBase
    {
        [Header("Time")]
        [SerializeField] private float _attackedFreezeTime;
        [Header("Animation")]
        [SerializeField] private AnimationClip _attackAnimation;
        [SerializeField] private AnimationClip _fallAnimation;
        [SerializeField] private AnimationClip _landingAnimation;
        [Header("Attack")]
        [SerializeField] private NetworkObject _shockwavePrefab;
        [SerializeField] private float _shockwaveMinSize;
        [SerializeField] private float _shockwaveScaleDuration;
        [SerializeField] private float _attackDuration;
        [SerializeField] private float _attackRange;
        [SerializeField] private int _damageAmount;
        [SerializeField] private float _knockBackPower;

        private NetworkObject _shockwaveObject;
        private List<NetworkObject> _attackTargetPlayers = new();
        private StampingState _stampingState;
        private float _slashEndTime; //振り下ろし終了時間
        private float _landedTime; //着地した時間
        private float _endTime; //アビリティの終了時間
        private float _attackEndTime; //攻撃判定の終了時間

        private GameObject _playerObject;
        private AnimationClipPlayer _animationClipPlayer;
        private AnimationClipPlayerManager _animationClipPlayerManager;
        private PlayerMovement _playerMovement;

        protected override void OnStart()
        {
            _stampingState = StampingState.Swing;
            _playerMovement.IgnoreMoveInput = true;
            _playerMovement.IgnoreEvasionInput = true;

            _animationClipPlayer.PlayClip(_attackAnimation);
            _animationClipPlayerManager.EnableFallMotion = false;

            _slashEndTime = Runner.SimulationTime + _attackAnimation.length;

            //自分以外を攻撃対象にする
            _attackTargetPlayers.Clear();
            foreach (var player in PlayerDatabase.Instance.PlayerObjectDic)
            {
                if (player.Value.gameObject == _playerObject)
                    continue;

                _attackTargetPlayers.Add(player.Value);
            }
        }

        protected override void OnUpdate(float deltaTime)
        {
            switch (_stampingState)
            {
                case StampingState.Swing:
                    Swing();
                    break;
                case StampingState.Falling:
                    Falling();
                    break;
                case StampingState.Landing:
                    Landing();
                    break;
            }
        }

        /// <summary>
        /// 振り下ろし中の処理
        /// </summary>
        private void Swing()
        {
            //振り下ろしが終了したら落下に移行する
            if (Runner.SimulationTime >= _slashEndTime)
            {
                _animationClipPlayer.PlayClipLoop(_fallAnimation);
                _stampingState = StampingState.Falling;
            }

            if (_playerMovement.IsGround)
                OnLanded();
        }

        /// <summary>
        /// 落下中の処理
        /// </summary>
        private void Falling()
        {
            if (_playerMovement.IsGround)
            {
                _animationClipPlayer.StopClip(_fallAnimation);
                OnLanded();
            }
        }

        /// <summary>
        /// 着地時の処理
        /// </summary>
        private void Landing()
        {
            //衝撃波を大きくする
            float t = Mathf.InverseLerp(_landedTime, _landedTime + _shockwaveScaleDuration, Runner.SimulationTime);
            float scale = Mathf.Lerp(_shockwaveMinSize, _attackRange * 2, t);
            _shockwaveObject.transform.localScale = Vector3.one * scale;

            //攻撃判定
            if (Runner.SimulationTime <= _attackEndTime)
            {
                float attackRange = scale / 2;

                HitboxDebugUtility.DrawWireSphere(_playerObject.transform.position, attackRange, Color.red);

                for (int i = _attackTargetPlayers.Count - 1; i >= 0; i--)
                {
                    NetworkObject player = _attackTargetPlayers[i];

                    //衝撃波に触れた
                    if ((_playerObject.transform.position - player.transform.position).sqrMagnitude < attackRange * attackRange)
                    {
                        //攻撃対象から外す
                        _attackTargetPlayers.Remove(player);

                        //ダメージ処理
                        if (player.TryGetComponent(out IDamageable damageable))
                        {
                            var hitData = new HitData(HitActionType.Damage, _damageAmount, _playerMovement.Object.InputAuthority, damageable.OwnerPlayerRef);
                            damageable.TakeHit(ref hitData);
                        }

                        //吹き飛ばす処理
                        if (player.TryGetComponent(out PlayerMovement movement))
                        {
                            var dir = movement.transform.position - _playerObject.transform.position;
                            var distance = dir.magnitude;

                            var power = _knockBackPower / Mathf.Max(distance, 0.1f);

                            movement.AddFlyingVelocity(dir.normalized * power);
                        }
                    }
                }
            }

            if (Runner.SimulationTime > _endTime)
            {
                _playerMovement.IgnoreMoveInput = false;
                _playerMovement.IgnoreEvasionInput = false;
                _animationClipPlayerManager.EnableFallMotion = true;
                Runner.Despawn(_shockwaveObject);
                RequestEndAbility();
            }
        }

        /// <summary>
        /// 着地時の処理
        /// </summary>
        private void OnLanded()
        {
            _animationClipPlayer.PlayClip(_landingAnimation);
            _endTime = Runner.SimulationTime + Mathf.Max(_shockwaveScaleDuration, _attackedFreezeTime);
            _landedTime = Runner.SimulationTime;
            _attackEndTime = Runner.SimulationTime + _attackDuration;

            Vector3 feetPosition = _playerMovement.MoveCapsuleCollider.bounds.min + Vector3.up * 0.1f;
            _shockwaveObject = Runner.Spawn(_shockwavePrefab, feetPosition);
            _shockwaveObject.transform.localScale = Vector3.one * _shockwaveMinSize;

            _stampingState = StampingState.Landing;
        }

        public override void SetPlayerComponent(GameObject player)
        {
            _playerObject = player;
            _playerMovement = player.GetComponent<PlayerMovement>();
            _animationClipPlayer = player.GetComponent<AnimationClipPlayer>();
            _animationClipPlayerManager = player.GetComponent<AnimationClipPlayerManager>();
        }

        public enum StampingState
        {
            Swing,//振り下ろし
            Falling,  // 落ちる
            Landing   // 着地
        }
    }
}
