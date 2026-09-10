using September.Common;
using UnityEngine;

namespace September.InGame.Kraken.Attack
{
    /// <summary>
    /// カメラの視線から攻撃目標地点を求める
    /// </summary>
    public class KrakenAimPointResolver
    {
        private readonly LayerMask _hitLayer;

        private Camera _localCamera;

        /// <param name="hitLayer"> 目標地点の判定に使うレイヤー </param>
        public KrakenAimPointResolver(LayerMask hitLayer)
        {
            _hitLayer = hitLayer;
        }

        /// <summary>
        /// 現在の視線の先の攻撃目標地点を求める
        /// </summary>
        /// <returns> カメラが取得できなかった場合と目標地点を見つけられなかった場合 false </returns>
        public bool TryResolveLocal(out KrakenAimPoint aimPoint)
        {
            if (_localCamera == null) _localCamera = Camera.main;

            if (_localCamera == null)
            {
                aimPoint = default;
                return false;
            }

            Transform cameraTransform = _localCamera.transform;
            Vector3 origin = cameraTransform.position;
            Vector3 forward = cameraTransform.forward;

            return TryResolve(origin, forward, out aimPoint);
        }

        /// <summary>
        /// 現在の視線の先の攻撃目標地点を求める
        /// </summary>
        /// <returns> 目標地点を見つけられなかった場合 false </returns>
        public bool TryResolveNetwork(PlayerInput input, out KrakenAimPoint aimPoint)
        {
            Vector3 origin = input.CameraPosition;
            Vector3 forward = input.DesiredLookDirection;

            return TryResolve(origin, forward, out aimPoint);
        }

        private bool TryResolve(Vector3 origin, Vector3 forward, out KrakenAimPoint aimPoint)
        {
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
