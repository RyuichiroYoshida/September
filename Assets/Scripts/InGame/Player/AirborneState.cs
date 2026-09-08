using Fusion;
using UnityEngine;

namespace InGame.Player
{
    /// <summary>
    /// 落下・空中慣性・外力の同期状態。
    /// 速度そのものではなく「いつ接地を離れたか」「いつ外力を受けたか」を Tick で保持することで、
    /// 入力権限側の予測 (再シミュレーション) でも決定的に再計算できる。
    /// <para>
    /// 速度を毎 Tick 累積する持ち方だと、再シミュレーションで同じ Tick が複数回実行されるたびに
    /// 重力や減衰が多重適用され、クライアントだけがホストと乖離する。
    /// </para>
    /// </summary>
    public struct AirborneState : INetworkStruct
    {
        /// <summary> 最後に地面へ接地していた Tick </summary>
        public int LastGroundedTick;
        /// <summary> 接地していた最後の Tick での水平速度。空中ではこれを減衰させた値を慣性として使う </summary>
        public Vector3 TakeoffVelocity;
        /// <summary> 外力 (ノックバック・爆風・アビリティ) を受けた瞬間の速度 </summary>
        public Vector3 ExternalVelocity;
        /// <summary> 外力を受けた Tick </summary>
        public int ExternalVelocityTick;
    }
}
