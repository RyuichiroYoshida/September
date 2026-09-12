using Fusion;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace InGame.Player.Sarutobi
{
    public class AbilitySarutobiUltRPCInvoker : NetworkBehaviour
    {
        [SerializeField] private CameraController _cameraController;
        [SerializeField] private PlayableDirector _playableDirector;
        [SerializeField] private TimelineAsset _startTimeline;
        [SerializeField] private TimelineAsset _endTimeline;
        [SerializeField] private float _cameraInDuration = 0f;
        [SerializeField] private float _cameraOutDuration = 1f;

        [Rpc]
        public void RPC_ChangeCameraOffset(Vector3 offset)
        {
            _cameraController.ChangeOffset(offset, 　_cameraInDuration);
        }

        [Rpc]
        public void RPC_ResetCameraOffset()
        {
            _cameraController.ResetOffset(_cameraOutDuration);
        }

        [Rpc]
        public void RPC_SetEndTimeline()
        {
            _playableDirector.playableAsset = _endTimeline;
            _playableDirector.Play();
        }

        [Rpc]
        public void RPC_SetStartTimeline()
        {
            _playableDirector.playableAsset = _startTimeline;
        }
    }
}
