using Fusion;
using InGame.Interact;
using InGame.Player;
using September.Common;
using September.InGame.Common;
using UnityEngine;

namespace InGame.exhibit
{
    public class HealViolin : InteractableBase
    {
        protected override void OnInteract(IInteractableContext context)
        {
            if(!HasStateAuthority)
                return;
            var requester = PlayerRef.FromEncoded(context.Interactor);
            var playerHealth = StaticServiceLocator.Instance.Get<InGameManager>().PlayerDataDic[requester].GetComponent<PlayerHealth>();
        }
    }
}
