using UnityEngine;

namespace September.InGame.Kraken.Attack
{
    /// <summary>
    /// クラーケン操作中に攻撃目標地点を示すマーカー。
    /// パーティクルはクライアント側でローカル生成するため他プレイヤーからは見えない。
    /// </summary>
    public class KrakenAimMarker : MonoBehaviour
    {
        [SerializeField, Tooltip("目標地点に表示するパーティクル")]
        private ParticleSystem _markerPrefab;

        [SerializeField, Tooltip("目標地点の面から浮かせる距離")]
        private float _surfaceOffset = 0.1f;

        [SerializeField, Tooltip("目標地点の面法線に合わせてマーカーを傾ける")]
        private bool _alignToSurface = true;

        private ParticleSystem _marker;
        private bool _isShown;

        /// <summary>
        /// 指定の目標地点にマーカーを表示する
        /// </summary>
        public void Show(in KrakenAimPoint aimPoint)
        {
            if (!TryGetMarker(out ParticleSystem marker)) return;

            Transform markerTransform = marker.transform;
            markerTransform.position = aimPoint.Position + aimPoint.Normal * _surfaceOffset;
            markerTransform.rotation = _alignToSurface
                ? Quaternion.FromToRotation(Vector3.up, aimPoint.Normal)
                : Quaternion.identity;

            if (_isShown) return;

            _isShown = true;
            marker.gameObject.SetActive(true);
            marker.Play(true);
        }

        /// <summary>
        /// マーカーを非表示にする
        /// </summary>
        public void Hide()
        {
            if (!_isShown) return;

            _isShown = false;

            if (_marker == null) return;

            _marker.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _marker.gameObject.SetActive(false);
        }

        private bool TryGetMarker(out ParticleSystem marker)
        {
            if (_marker == null && _markerPrefab != null)
            {
                _marker = Instantiate(_markerPrefab);
                _marker.gameObject.SetActive(false);
            }

            marker = _marker;
            return marker != null;
        }

        private void OnDestroy()
        {
            if (_marker != null) Destroy(_marker.gameObject);
        }
    }
}
