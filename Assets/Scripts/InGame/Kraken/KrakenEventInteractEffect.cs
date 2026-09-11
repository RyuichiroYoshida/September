using Fusion;
using InGame.Interact;
using UnityEngine;

namespace September.InGame.Kraken
{
    public class KrakenEventInteractEffect : CharacterInteractEffectBase
    {
        [SerializeField] private KrakenEventHandler _krakenEventHandler;
        [SerializeField] private InteractableBase _interactable;
        
        private Kraken _kraken;
        private PlayerRef _player;

        public override void OnInteractStart(IInteractableContext context, InteractableBase target)
        {
            if (!_krakenEventHandler.StartEvent(out _kraken))
            {
                _interactable.EndInteract();
                return;
            }
            
            _player = PlayerRef.FromEncoded(context.Interactor);

            _interactable.ForceSetInteractable = false;
        }

        public override void OnInteractEnd()
        {
            _interactable.EndInteract();
            _interactable.ForceSetInteractable = true;
        }

        public override CharacterInteractEffectBase Clone()
        {
            return MemberwiseClone() as CharacterInteractEffectBase;
        }
    }
}