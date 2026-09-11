using System;
using Fusion;
using InGame.Interact;
using September.Common.Attribute;
using UnityEngine;

namespace September.InGame.Mountable
{
    public class MountableRideInteractEffect : CharacterInteractEffectBase
    {
        [SerializeField, RequireInterface(typeof(IMountable))] private MonoBehaviour _targetMountable;
        
        private IMountable _mountable;
        private InteractableBase _interactable;

        public override void OnInteractStart(IInteractableContext context, InteractableBase target)
        {
            if (_mountable == null)
            {
                if (_targetMountable is not IMountable mountable)
                {
                    throw new InvalidOperationException($"Target must be a Mountable: {(target ? target.ToString() : "Null")}");
                }
                
                _mountable = mountable;
            }
            
            var player = PlayerRef.FromEncoded(context.Interactor);
            _mountable.GetOn(player);
            _interactable = target;
            _interactable.ForceSetInteractable = false;
        }

        public override void OnInteractEnd()
        {
            _interactable.ForceSetInteractable = true;
        }

        public override CharacterInteractEffectBase Clone()
        {
            return MemberwiseClone() as CharacterInteractEffectBase;
        }
    }
}