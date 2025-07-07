using InGame.Interact;

namespace InGame.Exhibit
{
    public class CursedBladeExhibit : CharacterInteractEffectBase
    {
        public float InteractTimer = 10.0f;
        public float AttackPower = 10.0f;
        
        public override void OnInteractStart(IInteractableContext context, InteractableBase target)
        {
            // キャラクター装備
        }

        public override CharacterInteractEffectBase Clone()
        {
            throw new System.NotImplementedException();
        }
    }
}