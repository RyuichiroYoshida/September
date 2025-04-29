using UnityEngine;

namespace September.InGame
{
    public class RideAbility : BaseAbility
    {
        public void HidePlayer()
        {
            gameObject.SetActive(false);
        }

        public void AppearPlayer(Transform appearTransform)
        {
            PlayerCameraController.LookHeadTransform();
            gameObject.SetActive(true);
            Rigidbody.position = appearTransform.position;
        }
        public override void Attack()
        {
        }

        public override AbilityType GetAbilityType() => AbilityType.Ride;
        
        public override void Use() { }
        
        public override void InteractWith(ExhibitBase exhibit)
        {
            exhibit.Interact(this);
        }
    }
}