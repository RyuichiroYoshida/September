using System;
using Fusion;
using InGame.Interact;
using September.Common;
using September.Common.Attribute;
using September.Common.Input;
using UnityEngine;

namespace September.InGame.Mountable
{
    /// <summary>
    /// ボタン入力でマウントを解除する効果
    /// </summary>
    public class MountableManualGetOffInteractEffect : CharacterInteractEffectBase
    {
        [SerializeField, RequireInterface(typeof(IMountable))] private MonoBehaviour _targetMountable;
        
        private IMountable _mountable;
        private InputWrapper _interactKey;
        private PlayerRef _currentOwner;
        private InteractableBase _interactable;

        public override void OnInteractStart(IInteractableContext context, InteractableBase target)
        {
            if (_mountable == null)
            {
                if (_targetMountable is not IMountable mountable)
                {
                    throw new InvalidOperationException($"Target must be a IMountable: {(target ? target.ToString() : "Null")}");
                }
                
                _mountable = mountable;
            }
            
            _currentOwner = PlayerRef.FromEncoded(context.Interactor);
            _interactable = target;
            _interactKey.SetInput(true);
        }

        public override void OnInteractFixedNetworkUpdate(PlayerInput playerInput)
        {
            if (_mountable == null) return;
            
            _interactKey.SetInput(playerInput.Buttons.IsSet(PlayerButtons.Interact));

            if (_interactKey.IsJustPressed)
            {
                _mountable.GetOff(_currentOwner);
                _interactable.EndInteract();
            }
        }

        public override CharacterInteractEffectBase Clone()
        {
            return MemberwiseClone() as CharacterInteractEffectBase;
        }
    }
}