using Unity.Cinemachine;
using UnityEngine;

namespace September.Common
{
    /// <summary>
    /// 現在のカメラ姿勢を解決する。
    /// Cinemachine の出力先 (Camera.main) は CinemachineBrain が LateUpdate で書き込むため、
    /// 入力収集時 (OnInput) に Camera.main を読むと必ず 1 フレーム前の姿勢になる。
    /// そこで Brain がアクティブにしている VirtualCamera 自身の Transform を読む。
    /// VirtualCamera はカメラ操作の直後に Transform が確定しているので、Brain の反映を待たずに最新姿勢が取れる。
    /// (Follow / LookAt で procedural に動く VirtualCamera でも、その Transform は Brain 更新時に
    /// 算出結果へ書き戻されるため、Camera.main を読む従来方式より遅れることはない)
    /// </summary>
    public sealed class CameraViewResolver
    {
        private CinemachineBrain _brain;

        public bool TryResolve(out CameraView view)
        {
            var source = ResolveSourceTransform();
            if (source == null)
            {
                view = default;
                return false;
            }

            view = CameraView.FromTransform(source);
            return true;
        }

        private Transform ResolveSourceTransform()
        {
            var brain = ResolveBrain();
            if (brain != null && brain.ActiveVirtualCamera is CinemachineVirtualCameraBase virtualCamera && virtualCamera != null)
            {
                return virtualCamera.transform;
            }

            // Brain が無い / VirtualCamera が未選択のシーンでは Unity カメラをそのまま使う (従来と同じ挙動)
            var mainCamera = Camera.main;
            return mainCamera != null ? mainCamera.transform : null;
        }

        private CinemachineBrain ResolveBrain()
        {
            // シーン遷移で Brain が破棄されることがあるので、無効になったら取り直す
            if (_brain != null && _brain.isActiveAndEnabled) return _brain;

            _brain = CinemachineBrain.ActiveBrainCount > 0 ? CinemachineBrain.GetActiveBrain(0) : null;
            return _brain;
        }
    }
}
