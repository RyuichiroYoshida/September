using Fusion;
using UnityEngine;

namespace InGame.Interact.Test
{
    [RequireComponent(typeof(NetworkObject))]
    public class ExampleSwitch : InteractableBase
    {
        protected override void OnInteract(IInteractableContext context)
        {
            Debug.Log($"[ExampleSwitch] {context.Interactor} がインタラクトしました");
            // 実処理...
        }
    }

}
