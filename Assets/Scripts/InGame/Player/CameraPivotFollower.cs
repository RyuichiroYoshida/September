using UnityEngine;

namespace InGame.Player
{
    /// <summary>
    /// CameraPivot を NetworkRigidbody の補間ターゲット (見えているメッシュ) に追従させる。
    /// <para>
    /// プレイヤーの Root Transform は FixedUpdateNetwork で確定した最新 Tick の位置にあるが、
    /// 描画されるメッシュは Fusion の Render 補間で「1 つ前の Tick と最新 Tick の間」に置かれる。
    /// CameraPivot は Root の子なので、そのままでは見えているキャラクターより最大 1 Tick 分先行し、
    /// 移動中にキャラクターがカメラに対して毎フレーム前後にズレて見える。
    /// LateUpdate (Render 後) で Pivot の位置だけを補間ターゲット基準に置き直して揃える。
    /// 回転は CameraController がワールド角で直接書くため影響しない。
    /// </para>
    /// </summary>
    public sealed class CameraPivotFollower
    {
        private readonly Transform _pivot;
        private readonly Transform _target;
        /// <summary> 追従先から見た Pivot の相対位置 (Prefab 初期状態で確定) </summary>
        private readonly Vector3 _offsetFromTarget;
        /// <summary> 追従しないときに戻す Root 基準のローカル位置 </summary>
        private readonly Vector3 _defaultLocalPosition;

        public CameraPivotFollower(Transform pivot, Transform target)
        {
            _pivot = pivot;
            _target = target;
            _offsetFromTarget = target.InverseTransformPoint(pivot.position);
            _defaultLocalPosition = pivot.localPosition;
        }

        /// <summary>
        /// Pivot の位置を補間ターゲット基準に更新する。
        /// 補間ターゲットが非アクティブ (メッシュ非表示中) のときは Fusion が更新しないので Root 基準へ戻す。
        /// </summary>
        public void Apply()
        {
            if (_target == null || !_target.gameObject.activeInHierarchy)
            {
                _pivot.localPosition = _defaultLocalPosition;
                return;
            }

            _pivot.position = _target.TransformPoint(_offsetFromTarget);
        }
    }
}
