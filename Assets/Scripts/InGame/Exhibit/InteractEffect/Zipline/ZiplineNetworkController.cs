using Fusion;
using InGame.Player;
using UnityEngine;

namespace September
{
    // ジップラインの台車姿勢と乗降状態を同期する。
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public class ZiplineNetworkController : NetworkBehaviour
    {
        // 台車の姿勢と乗車プレイヤーをホストから各クライアントへ共有する。
        [Networked] private NetworkBool ZipPoseValid { get; set; }
        [Networked] private Vector3 ZipPosition { get; set; }
        [Networked] private Quaternion ZipRotation { get; set; }
        [Networked] private NetworkObject ZipRider { get; set; }
        [SerializeField] private Transform _trolley;
        [Networked] private Vector3 PlayerOffset { get; set; }
        private PlayerManager _shownZipRider;
        private NetworkTransform _zipTransform;
        private bool _zipTransformWasEnabled;
        private bool _receivedZipPose;

        public override void Spawned()
        {
            if (_trolley == null) return;
            // 台車の座標更新はこの同期処理にまとめる。
            _zipTransform = _trolley.GetComponent<NetworkTransform>();
            if (_zipTransform != null)
            {
                _zipTransformWasEnabled = _zipTransform.enabled;
                _zipTransform.enabled = false;
            }
            if (HasStateAuthority) PublishZiplinePose(_trolley, null);
        }

        public void PublishZiplinePose(Transform trolley, NetworkObject rider, Vector3 offset = default)
        {
            if (!HasStateAuthority) return;
            PlayerOffset = offset;
            ZipPosition = trolley.position;
            ZipRotation = trolley.rotation;
            ZipRider = rider;
            ZipPoseValid = true;
        }

        public override void Render()
        {
            if (HasStateAuthority || _trolley == null || !ZipPoseValid) return;
            var trolley = _trolley;
            // 受信した台車の位置・回転へ表示を補間する。初回は即座に合わせる。
            float blend = _receivedZipPose ? 1f - Mathf.Exp(-20f * Time.deltaTime) : 1f;
            trolley.SetPositionAndRotation(Vector3.Lerp(trolley.position, ZipPosition, blend),
                Quaternion.Slerp(trolley.rotation, ZipRotation, blend));
            _receivedZipPose = true;

            PlayerManager rider = ZipRider != null ? ZipRider.GetComponent<PlayerManager>() : null;
            if (rider == _shownZipRider) return;
            ReleaseZiplineView();
            if (rider == null) return;
            _shownZipRider = rider;
            rider.BeginRideTracking(trolley, PlayerOffset);
        }

        private void ReleaseZiplineView()
        {
            if (_shownZipRider == null) return;
            _shownZipRider.EndRideTracking();
            _shownZipRider = null;
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            ReleaseZiplineView();
            _receivedZipPose = false;
            if (_zipTransform != null) _zipTransform.enabled = _zipTransformWasEnabled;
        }

    }
}
