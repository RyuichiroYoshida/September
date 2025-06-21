using Fusion;
using September.Common;
using UnityEngine;

namespace InGame.Interact.Test
{
    [RequireComponent(typeof(NetworkObject))]
    public class ExampleSwitch : InteractableBase
    {
        private bool _isInUse;

        protected override bool OnValidateInteraction(IInteractableContext context, CharacterType charaType)
        {
            return true;
        }

        protected override void OnInteract(IInteractableContext context)
        {
            _isInUse = true;
            Debug.Log($"[ExampleSwitch] {context.Interactor} がインタラクトしました");
            // 実処理...
        }
    }

}
