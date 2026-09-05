using UnityEngine;

namespace September.Common
{
    /// <summary>
    /// 入力に載せるカメラの姿勢 (位置・前方・ヨー角) のスナップショット
    /// </summary>
    public readonly struct CameraView
    {
        public readonly Vector3 Position;
        public readonly Vector3 Forward;
        /// <summary> 0 <= Yaw < 360 (Transform.rotation.eulerAngles.y と同じ規約) </summary>
        public readonly float Yaw;

        public CameraView(Vector3 position, Vector3 forward, float yaw)
        {
            Position = position;
            Forward = forward;
            Yaw = yaw;
        }

        public static CameraView FromTransform(Transform source)
        {
            return new CameraView(source.position, source.forward.normalized, source.rotation.eulerAngles.y);
        }
    }
}
