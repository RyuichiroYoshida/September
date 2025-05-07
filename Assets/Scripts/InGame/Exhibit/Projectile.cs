using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;

namespace September.InGame
{
    public class Projectile : NetworkBehaviour
    {
        [SerializeField] float _lifeTime = 5f;
        [SerializeField] bool _isTrigger;
        [SerializeField] NetworkRigidbody3D _networkRb;
        public NetworkRigidbody3D NetworkRb => _networkRb;
        public PlayerController Owner { get; set; }
        private void Start()
        {
            Destroy(gameObject, _lifeTime);
        }

        private void OnTriggerEnter(Collider other)
        {
            if(!_isTrigger)
                return;
            
            if (other.TryGetComponent(out PlayerController playerController))
            {
                playerController.TakeDamage(Owner, Owner.Data.AttackDamage);
            }
        }

        private void OnCollisionEnter(Collision other)
        {
            if(_isTrigger) return;
            if (other.gameObject.TryGetComponent(out PlayerController playerController))
            {
                playerController.TakeDamage(Owner, Owner.Data.AttackDamage);
            }
        }
    }
}