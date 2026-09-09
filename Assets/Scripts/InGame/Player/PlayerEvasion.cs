using UnityEngine;

namespace InGame.Player
{
    /// <summary>
    /// 回避の開始判定と、Tick 基準での移動速度・向き・無敵判定の計算を行う。
    /// 状態そのものは Networked な <see cref="EvasionState"/> が持ち、このクラスは計算のみを担当する。
    /// </summary>
    public class PlayerEvasion
    {
        private readonly EvasionData _evasionData;

        public PlayerEvasion(EvasionData evasionData)
        {
            if (evasionData == null)
            {
                Debug.LogError("[PlayerEvasion] EvasionData is null");
            }
            _evasionData = evasionData;
        }

        /// <summary>
        /// 回避を開始できるなら state を更新して true を返す。
        /// </summary>
        public bool TryStartEvasion(ref EvasionState state, Vector2 inputDirection, Vector3 currentForward, int currentTick, float tickDeltaTime, int playerWeight)
        {
            if (state.IsEvading)
                return false;

            // クールダウン中は開始できない
            if (state.LastEndTick > 0 && (currentTick - state.LastEndTick) * tickDeltaTime < _evasionData.Cooldown)
                return false;

            // 入力がないならプレイヤーの向きを入力方向として扱う
            if (inputDirection.magnitude < Mathf.Epsilon)
                inputDirection = new Vector2(currentForward.x, currentForward.z);

            // 移動方向を求める
            var targetDirection = new Vector2(currentForward.x, currentForward.z);
            var angle = Vector2.SignedAngle(inputDirection, targetDirection);
            var clampedAngle = Mathf.Clamp(angle, -_evasionData.InputAngle, _evasionData.InputAngle);
            var moveDirection = Quaternion.Euler(0, clampedAngle, 0) * currentForward;
            moveDirection.y = 0f;
            moveDirection = moveDirection.normalized;

            float weightCoefficient = CalculateWeightCoefficient(playerWeight);
            float turnProgress = Mathf.InverseLerp(0, _evasionData.InputAngle, Mathf.Abs(clampedAngle));

            state.IsEvading = true;
            state.StartTick = currentTick;
            state.RollDuration = _evasionData.RollDuration * weightCoefficient;
            // 向き変更がロールより長いと移動方向を向き切らないままロールが終わり、モーションの向きと実際の移動方向がずれる
            state.TurnDuration = Mathf.Min(_evasionData.MaxTurnDuration * turnProgress * weightCoefficient, state.RollDuration);
            state.RollDistance = _evasionData.RollDistance * weightCoefficient;
            state.MoveDirection = moveDirection;
            state.StartDirection = currentForward;

            return true;
        }

        /// <summary> ロールの継続時間が経過したか </summary>
        public bool HasEnded(in EvasionState state, int currentTick, float tickDeltaTime)
        {
            return ElapsedTime(in state, currentTick, tickDeltaTime) >= state.RollDuration;
        }

        /// <summary>
        /// 直前 Tick からの移動距離差分を時間で割った水平速度。Rigidbody の速度としてそのまま適用できる。
        /// </summary>
        public Vector3 CalcVelocity(in EvasionState state, int currentTick, float tickDeltaTime)
        {
            float previousDistance = state.RollDistance * RollProgress(in state, currentTick - 1, tickDeltaTime);
            float currentDistance = state.RollDistance * RollProgress(in state, currentTick, tickDeltaTime);

            return state.MoveDirection * ((currentDistance - previousDistance) / tickDeltaTime);
        }

        /// <summary> 現在の Tick における正面方向 </summary>
        public Vector3 CalcForward(in EvasionState state, int currentTick, float tickDeltaTime)
        {
            float t = state.TurnDuration > 0f
                ? Mathf.Clamp01(ElapsedTime(in state, currentTick, tickDeltaTime) / state.TurnDuration)
                : 1f;
            float speedT = _evasionData.TurnSpeedCurve.Evaluate(t);

            Vector3 currentDirection = Vector3.Slerp(state.StartDirection, state.MoveDirection, speedT);
            currentDirection.y = 0f;

            return currentDirection.sqrMagnitude > 0f ? currentDirection.normalized : state.MoveDirection;
        }

        /// <summary> 現在の Tick が無敵時間内か </summary>
        public bool IsInvincible(in EvasionState state, int currentTick, float tickDeltaTime)
        {
            float evasionTime = ElapsedTime(in state, currentTick, tickDeltaTime);
            return evasionTime >= _evasionData.StartInvincibleTime && evasionTime <= _evasionData.InvincibleTime;
        }

        /// <summary> 回避開始からの経過時間 (秒) </summary>
        private static float ElapsedTime(in EvasionState state, int currentTick, float tickDeltaTime)
        {
            return (currentTick - state.StartTick) * tickDeltaTime;
        }

        /// <summary> ロール全体の移動距離に対する進捗率 (カーブ適用後) </summary>
        private float RollProgress(in EvasionState state, int currentTick, float tickDeltaTime)
        {
            if (state.RollDuration <= 0f)
                return 1f;

            float t = Mathf.Clamp01(ElapsedTime(in state, currentTick, tickDeltaTime) / state.RollDuration);
            return _evasionData.RollSpeedCurve.Evaluate(t);
        }

        /// <summary> 重み係数計算 </summary>
        private float CalculateWeightCoefficient(int jewelryCount)
        {
            return 1f - (jewelryCount * _evasionData.WeightDecay);
        }
    }
}
