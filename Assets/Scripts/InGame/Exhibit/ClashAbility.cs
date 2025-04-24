namespace September.InGame
{
    public class ClashAbility : IAbility
    {
        public AbilityType GetAbilityType() => AbilityType.Clash;
        
        public void Use() { }

        public void InteractWith(ExhibitBase exhibit)
        {
            exhibit.Interact(this);
        }
    }
}