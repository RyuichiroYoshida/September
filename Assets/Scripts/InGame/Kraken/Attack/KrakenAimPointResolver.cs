using UnityEngine;

namespace September.InGame.Kraken.Attack
{
    /// <summary>
    /// カメラの視線から攻撃目標地点を求める
    /// </summary>
    public class KrakenAimPointResolver
    {
        private readonly LayerMask _hitLayer;

        private Camera _camera;

        /// <param name="hitLayer"> 目標地点の判定に使うレイヤー </param>
        public KrakenAimPointResolver(LayerMask hitLayer)
        {
            _hitLayer = hitLayer;
        }

        /// <summary>
        /// 現在の視線の先の攻撃目標地点を求める
        /// </summary>
        /// <returns> カメラが取得できなかった場合は false </returns>
        public bool TryResolve(out KrakenAimPoint aimPoint)
        {
            if (_camera == null) _camera = Camera.main;

            if (_camera == null)
            {
                aimPoint = default;
                return false;
            }

            Transform cameraTransform = _camera.transform;
            Vector3 origin = cameraTransform.position;
            Vector3 forward = cameraTransform.forward;

            if (Physics.Raycast(origin, forward, out RaycastHit hit, Mathf.Infinity, _hitLayer))
            {
                aimPoint = new KrakenAimPoint(hit.point, hit.normal, true);
                return true;
            }

            aimPoint = default;
            return false;
        }
    }

    /// <summary>
    /// 攻撃目標地点の情報
    /// </summary>
    public readonly struct KrakenAimPoint
    {
        /// <summary> 目標地点のワールド座標 </summary>
        public readonly Vector3 Position;

        /// <summary> 目標地点の面法線。何にも当たっていない場合は上方向 </summary>
        public readonly Vector3 Normal;

        /// <summary> 視線の先に地形などが存在したか </summary>
        public readonly bool HasSurface;

        public KrakenAimPoint(Vector3 position, Vector3 normal, bool hasSurface)
        {
            Position = position;
            Normal = normal;
            HasSurface = hasSurface;
        }
    }
}
