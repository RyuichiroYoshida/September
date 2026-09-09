using UnityEngine;

namespace InGame.Player
{
    /// <summary>
    /// 空中の落下速度・慣性・外力を Tick 基準で計算する。
    /// 状態そのものは Networked な <see cref="AirborneState"/> が持ち、このクラスは計算のみを担当する。
    /// <para>
    /// すべての値を「経過 Tick 数の純関数」として求めるため、同じ Tick を何度再シミュレーションしても
    /// 結果が変わらない。毎 Tick 加算・減衰させる実装は再シミュレーションで多重適用されるので使わない。
    /// </para>
    /// </summary>
    public class AirborneMotion
    {
        /// <summary> 接地が切れてから空中扱いになるまでの猶予 (秒) </summary>
        private readonly float _coyoteTime;
        /// <summary> 空中の水平慣性の減衰係数 </summary>
        private readonly float _moveDamping;
        /// <summary> 外力の減衰係数 </summary>
        private readonly float _externalDamping;

        /// <summary> この二乗速度未満まで減衰した外力は 0 として扱う (指数減衰は 0 に到達しないため) </summary>
        private const float ExternalVelocityCutoffSqr = 0.001f;

        public AirborneMotion(float coyoteTime, float moveDamping, float externalDamping)
        {
            _coyoteTime = coyoteTime;
            _moveDamping = moveDamping;
            _externalDamping = externalDamping;
        }

        /// <summary> 実接地が切れてからの経過秒 </summary>
        public float TimeSinceGrounded(in AirborneState state, int tick, float tickDeltaTime)
            => Mathf.Max(0, tick - state.LastGroundedTick) * tickDeltaTime;

        /// <summary> コヨーテタイム中か (接地扱いを継続するか) </summary>
        public bool IsWithinCoyoteTime(in AirborneState state, int tick, float tickDeltaTime)
            => TimeSinceGrounded(in state, tick, tickDeltaTime) <= _coyoteTime;

        /// <summary> コヨーテタイムを消化して実際に空中扱いになってからの経過 Tick 数 </summary>
        public int AirborneTicks(in AirborneState state, int tick, float tickDeltaTime)
            => Mathf.Max(0, tick - state.LastGroundedTick - CoyoteTicks(tickDeltaTime));

        /// <summary> 重力による落下速度 </summary>
        public Vector3 CalcFallVelocity(in AirborneState state, int tick, float tickDeltaTime)
            => Physics.gravity * (AirborneTicks(in state, tick, tickDeltaTime) * tickDeltaTime);

        /// <summary> 離陸時の水平速度を減衰させた空中の慣性 </summary>
        public Vector3 CalcAirMoveVelocity(in AirborneState state, int tick, float tickDeltaTime)
            => state.TakeoffVelocity * DampingFactor(_moveDamping, AirborneTicks(in state, tick, tickDeltaTime), tickDeltaTime);

        /// <summary> 減衰後の外力 </summary>
        public Vector3 CalcExternalVelocity(in AirborneState state, int tick, float tickDeltaTime)
        {
            if (state.ExternalVelocity == Vector3.zero) return Vector3.zero;

            int elapsedTicks = Mathf.Max(0, tick - state.ExternalVelocityTick);
            Vector3 velocity = state.ExternalVelocity * DampingFactor(_externalDamping, elapsedTicks, tickDeltaTime);

            return velocity.sqrMagnitude < ExternalVelocityCutoffSqr ? Vector3.zero : velocity;
        }

        /// <summary>
        /// 接地した Tick と、そのときの水平速度を記録する。
        /// <para>
        /// <b>実際に地面へ接触している Tick でのみ呼ぶこと。</b>
        /// <see cref="IsWithinCoyoteTime"/> が真という理由で呼ぶと、接地判定が自身の記録した
        /// Tick を参照して毎 Tick 更新され続け、永久に接地扱い (空中歩行) になる。
        /// </para>
        /// </summary>
        public void MarkGrounded(ref AirborneState state, int tick, Vector3 horizontalVelocity)
        {
            state.LastGroundedTick = tick;
            state.TakeoffVelocity = horizontalVelocity;
        }

        /// <summary>
        /// 離陸時の初速だけを更新する。基準 Tick は動かさないのでコヨーテタイム中に呼んでも安全。
        /// </summary>
        public void CaptureTakeoffVelocity(ref AirborneState state, Vector3 horizontalVelocity)
            => state.TakeoffVelocity = horizontalVelocity;

        /// <summary> コヨーテタイムを打ち切り、次の評価から空中扱いにする </summary>
        public void CancelCoyoteTime(ref AirborneState state, int tick, float tickDeltaTime)
        {
            // 猶予ぶんだけ過去へ倒すと TimeSinceGrounded がコヨーテタイムを超え、即座に落下が始まる
            state.LastGroundedTick = tick - CoyoteTicks(tickDeltaTime) - 1;
        }

        /// <summary> 外力の初速と適用 Tick を記録する </summary>
        public void SetExternalVelocity(ref AirborneState state, int tick, Vector3 velocity)
        {
            state.ExternalVelocity = velocity;
            state.ExternalVelocityTick = tick;
        }

        /// <summary> 落下・空中慣性・外力をすべて 0 に戻す </summary>
        public void Reset(ref AirborneState state, int tick)
        {
            state.LastGroundedTick = tick;
            state.TakeoffVelocity = Vector3.zero;
            state.ExternalVelocity = Vector3.zero;
            state.ExternalVelocityTick = tick;
        }

        /// <summary> コヨーテタイムを Tick 数へ換算する </summary>
        private int CoyoteTicks(float tickDeltaTime)
            => tickDeltaTime > 0f ? Mathf.CeilToInt(_coyoteTime / tickDeltaTime) : 0;

        /// <summary>
        /// <c>Vector3.Lerp(v, Vector3.zero, damping * dt)</c> を elapsedTicks 回適用した結果の倍率。
        /// 毎 Tick 減衰させず経過 Tick 数から一括で求めるので、再シミュレーションで多重適用されない
        /// </summary>
        private static float DampingFactor(float damping, int elapsedTicks, float tickDeltaTime)
            => Mathf.Pow(Mathf.Clamp01(1f - damping * tickDeltaTime), elapsedTicks);
    }
}
