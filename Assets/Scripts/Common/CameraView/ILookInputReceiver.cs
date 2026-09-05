using UnityEngine;

namespace September.Common
{
    /// <summary>
    /// ローカルプレイヤーの視点入力 (マウス / 右スティック) を受け取ってカメラを回すもの。
    /// 入力収集 (OnInput) の直前にも呼ばれるため、同一フレーム内で二重に回さないことを実装側が保証する。
    /// </summary>
    public interface ILookInputReceiver
    {
        /// <summary>
        /// このフレームの視点入力を適用する。
        /// </summary>
        /// <returns> 適用したら true。同一フレームで既に適用済みなら false </returns>
        bool TryApplyLookInput(Vector2 lookInput, float deltaTime);
    }
}
