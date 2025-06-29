using InGame.Interact;
using September.Common;
using UnityEngine;

namespace InGame.Interact
{
    [System.Serializable]
    public abstract class CharacterInteractEffectBase
    {
        protected CharacterInteractEffectBase () { }
        
        [SerializeField]
        private CharacterType _characterType = CharacterType.All;

        public CharacterType CharacterType => _characterType;
        public abstract void OnInteractStart(IInteractableContext context, InteractableBase target);
        public virtual void OnInteractUpdate(float deltaTime) { }
        public virtual void OnInteractLateUpdate(float deltaTime) { }
        public virtual void OnInteractFixedUpdate() { }
        public virtual void OnInteractFixedNetworkUpdate() { }
        public virtual void OnInteractCollisionStay(Collision collision) { }
        public virtual void OnInteractEnd() { }

        public abstract CharacterInteractEffectBase Clone();
    }
}
