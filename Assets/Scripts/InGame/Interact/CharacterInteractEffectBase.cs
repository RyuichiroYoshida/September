using InGame.Interact;
using UnityEngine;

namespace InGame.Interact
{
    [System.Serializable]
    public abstract class CharacterInteractEffectBase
    {
        public void OnInteractStart(IInteractableContext context, InteractableBase target) { }
        public virtual void OnInteractUpdate(float deltaTime) { }
        public virtual void OnInteractLateUpdate(float deltaTime) { }
        public virtual void OnInteractFixedUpdate() { }
        public virtual void OnInteractFixedNetworkUpdate() { }
        public virtual void OnInteractCollisionStay(Collision collision) { }
        public virtual void OnInteractEnd() { }

        public abstract CharacterInteractEffectBase Clone();
    }
}
