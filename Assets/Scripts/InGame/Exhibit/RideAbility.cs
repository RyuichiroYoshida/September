using Fusion;
using UnityEngine;

namespace September.InGame
{
    public class RideAbility : BaseAbility
    {
        public void HidePlayer()
        {
            RPC_HidePlayer();
        }
        
        [Rpc(RpcSources.All, RpcTargets.All)]
        private void RPC_HidePlayer()
        {
            gameObject.SetActive(false);
        }

        public void AppearPlayer(Transform appearTransform)
        {
            RPC_AppearPlayer(appearTransform.position);
        }

        [Rpc(RpcSources.All, RpcTargets.All)]
        private void RPC_AppearPlayer(Vector3 appearPosition)
        {
            PlayerCameraController.LookHeadTransform();
            gameObject.SetActive(true);
            Rigidbody.position = appearPosition;
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