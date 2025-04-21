namespace September.InGame
{
    public enum AbilityType
    {
        Ride,
        Clash
    }
    
    // Player側の呼び出し方　_ability.InteractWith(_currentExhibit);
    public interface IAbility
    {
        public void Use();
        public void InteractWith(ExhibitBase exhibit);
        AbilityType GetAbilityType();
    }
}
