using System;
using UnityEngine;

namespace InGame.Player.Ability
{
    [Serializable]
    public class AbilityMultiHitAttack : AbilityNormalAttack
    {
        [Header("2回目以降の攻撃フレーム")]
        [SerializeField] private AttackFrame[] _attackFrames;

        private int _attackCount;

        protected override void OnUpdate(float deltaTime)
        {
            int now    = Runner.Tick;
            int elapsed = now - _attackStartTick;

            _playerMovement.SetRotationDirection(_attackDirection);

            /*
            // 最も近い敵の方向を向く
            if (_closestEnemyTransform != null && _playerMovement != null)
            {
                Vector3 directionToEnemy = (_closestEnemyTransform.position - Parameter.Owner.transform.position).normalized;
                directionToEnemy.y = 0; // Y軸は無視して水平方向のみ

                if (directionToEnemy.magnitude > 0.1f)
                {
                    _playerMovement.SetRotationDirection(directionToEnemy);
                }
            }
            */

            // ヒット窓
            bool inWindow = elapsed >= _startHitTick && elapsed < _endHitTick;

            if (inWindow)
            {
                CastAndApplyHits(); // ← ここで BoxCast 実行
            }
            else if (elapsed >= _endHitTick)
            {
                // 窓を抜けたら次の攻撃に備えてクリア
                if (_alreadyHit.Count > 0)
                    _alreadyHit.Clear();
                
                // 次の攻撃フレームを設定する
                if (_attackCount < _attackFrames.Length)
                {
                    _startHitTick  = FrameToTick(_attackFrames[_attackCount].Start);
                    _endHitTick    = FrameToTick(_attackFrames[_attackCount].End);
                
                    _attackCount++;
                }
            }
            
            _playerMovement.AddForce(Parameter.Owner.transform.forward * _moveForwardSpeed);

            // 攻撃終了
            if (elapsed >= _endAttackTick)
            {
                _playerMovement.IgnoreMoveInput = false;
                _playerMovement.IgnoreEvasionInput = false;
                RequestEndAbility();
                
                _startHitTick  = FrameToTick(_startHitCheckFrame);
                _endHitTick    = FrameToTick(_endHitCheckFrame);
                _attackCount = 0;
            }
        }

        [Serializable]
        struct AttackFrame
        {
            public int Start;
            public int End;
        }
    }
}
