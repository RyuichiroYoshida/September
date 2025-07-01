using UnityEngine;

namespace InGame.Interact
{
    [System.Serializable]
    public class SimpleLogEffect : CharacterInteractEffectBase
    {
        public string effectName = "Default";
        
        public SimpleLogEffect() { }

        public override void OnInteractStart(IInteractableContext context, InteractableBase target)
        {
            Debug.Log($"[SimpleLogEffect] OnInteractStart: {effectName}");
        }

        public override CharacterInteractEffectBase Clone()
        {
            return new SimpleLogEffect { effectName = this.effectName };
        }
    }
}