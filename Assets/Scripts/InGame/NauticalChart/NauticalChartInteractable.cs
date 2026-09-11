using Fusion;
using UnityEngine;

namespace September.InGame.NauticalChart
{
    /// <summary> 海図のインタラクション効果を制御するクラス </summary>
    public class NauticalChartInteractable : NetworkBehaviour
    {
        [SerializeField] private float _duration = 3.0f;
        [SerializeField] private StormManager _stormManager;
        [SerializeReference, SubclassSelector] private IFogController _fogController;

        [Networked] private TickTimer TickTimer { get; set; }
        private bool _isFogActive;

        // === Host Only ===
        private bool _isInteracting;

        /// <summary> RPCでインタラクト時に霧と嵐を表示する </summary>
        /// <param name="interactPlayerRef"></param>
        [Rpc(RpcSources.All, RpcTargets.All)]
        public void RPC_OnInteractStart(PlayerRef interactPlayerRef)
        {
            TickTimer = TickTimer.CreateFromSeconds(Runner, _duration); // 霧の表示時間を設定

            if (Runner.LocalPlayer != interactPlayerRef)
            {
                _fogController.ShowFog();
                _isFogActive = true;
            }

            _stormManager.StartStorm(_duration, _duration); // 全員に嵐の表示

            if (HasStateAuthority)
            {
                _isInteracting = true;
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (_isInteracting && TickTimer.Expired(Runner) && _fogController != null)
            {
                RPC_OnInteractEnd();
                _isInteracting = false;
            }
        }

        [Rpc(RpcSources.All, RpcTargets.All)]
        private void RPC_OnInteractEnd()
        {
            Debug.Log("RPC_OnInteractEnd");
            if (_isFogActive)
            {
                _fogController.HideFog();
                _isFogActive = false;
            }
        }
    }
}
