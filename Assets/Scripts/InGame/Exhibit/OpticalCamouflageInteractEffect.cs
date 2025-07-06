using System;
using Cysharp.Threading.Tasks;
using Fusion;
using InGame.Interact;
using InGame.Player;

namespace InGame.Exhibit
{
    [Serializable]
    public class OpticalCamouflageInteractEffect : CharacterInteractEffectBase
    {
        public float _duration;
        public override void OnInteractStart(IInteractableContext context, InteractableBase target)
        {
            PlayerRef playerRef = PlayerRef.FromEncoded(context.Interactor);
            
            // Runnerからplayerを取得する
            if (target.Runner.TryGetPlayerObject(playerRef, out var playerNetworkObject))
            {
                StartOpticalCamouflage(playerNetworkObject);
                StopOpticalCamouflage(playerNetworkObject).Forget();
            }
        }

        public override CharacterInteractEffectBase Clone()
        {
            return new OpticalCamouflageInteractEffect
            {
                _duration = _duration,
            };
        }
        private void StartOpticalCamouflage(NetworkObject player)
        {
            if (player.TryGetComponent<PlayerRenderer>(out var playerRenderer))
            {
                playerRenderer.Rpc_SetOpticalCamouflageMaterial();
            }
        }

        private async UniTaskVoid StopOpticalCamouflage(NetworkObject player)
        {
            await UniTask.WaitForSeconds(_duration);
            if (player.TryGetComponent<PlayerRenderer>(out var playerRenderer))
            {
                playerRenderer.Rpc_ResetMaterial();
            }
        }
    }
}