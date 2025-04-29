using UnityEngine;

namespace September.InGame
{
    public class CannonBallExhibit : ExhibitBase
    {
        public override void Interact(BaseAbility baseAbility)
        {
            switch (baseAbility.GetAbilityType())
            {
                case AbilityType.Clash:
                    var clashAbility = baseAbility as ClashAbility;
                    clashAbility.HasCannonBall = true;
                    Destroy(gameObject);
                    break;
                default:
                    Debug.LogWarning("この展示物にはこのアビリティは使えません");
                    break;
            }
        }
    }
}