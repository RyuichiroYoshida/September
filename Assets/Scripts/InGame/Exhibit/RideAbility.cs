namespace September.InGame
{
    public class RideAbility : IAbility
    {
        public AbilityType GetAbilityType() => AbilityType.Ride;
        
        // 演出が必要なとき
        public void Use() { }

        // 展示物の種類によってAbilityを変更する
        public void InteractWith(ExhibitBase exhibit)
        {
            exhibit.Interact(this);
        }
    }
}