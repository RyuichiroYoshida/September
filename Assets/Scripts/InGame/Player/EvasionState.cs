using Fusion;
using UnityEngine;

namespace InGame.Player
{
    /// <summary>
    /// 回避 1 回分の同期状態。
    /// Tick 基準で保持することで、入力権限側の予測 (再シミュレーション) でも決定的に再計算できる。
    /// </summary>
    public struct EvasionState : INetworkStruct
    {
        /// <summary> 回避中か </summary>
        public NetworkBool IsEvading;
        /// <summary> 回避を開始した Tick </summary>
        public int StartTick;
        /// <summary> 直前の回避が終了した Tick (クールダウン判定用) </summary>
        public int LastEndTick;
        /// <summary> ロール全体の所要時間 (秒、重量係数適用後) </summary>
        public float RollDuration;
        /// <summary> 向き変更の所要時間 (秒、重量係数適用後) </summary>
        public float TurnDuration;
        /// <summary> ロール全体の移動距離 (m、重量係数適用後) </summary>
        public float RollDistance;
        /// <summary> ロールの移動方向 (水平・正規化済み) </summary>
        public Vector3 MoveDirection;
        /// <summary> 回避開始時の正面方向 </summary>
        public Vector3 StartDirection;
    }
}
