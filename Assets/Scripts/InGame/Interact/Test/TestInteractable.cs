using Fusion;
using UnityEngine;

namespace InGame.Interact.Test
{
    [RequireComponent(typeof(NetworkObject))]
    public class TestInteractable : NetworkBehaviour, IInteractable
    {
        [SerializeField] private string _itemName = "テストアイテム";

        public void Interact(IInteractableContext context)
        {
            Debug.Log($"[TestInteractable] {_itemName} が {PlayerRef.FromEncoded(context.Interactor)} によってインタラクトされました (位置: {context.WorldPosition})");
        }
    }
}
