using UnityEngine;

namespace September.InGame
{
    public class AirplaneExhibit : ExhibitBase
    {
        [SerializeField] private FlightController _flightController;
        
        // 呼び出すと飛行が始まる
        public override void Interact(IAbility ability)
        {
            switch (ability.GetAbilityType())
            {
                case AbilityType.Ride:
                    StartRide();
                    break;
                default:
                    Debug.Log("この展示物にはこのアビリティは使えません");
                    break;
            }
        }

        private void StartRide()
        {
            _flightController.StartFlight();
        }
    }
}