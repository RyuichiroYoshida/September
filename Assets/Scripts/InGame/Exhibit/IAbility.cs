namespace September.InGame
{
    public enum AbilityType
    {
        Ride,
        Clash
    }
    
    public interface IAbility
    {
        // 演出が必要なとき
        public void Use();
        // 展示物の種類によってAbilityを変更する
        public void InteractWith(ExhibitBase exhibit);
        AbilityType GetAbilityType();
    }
}
