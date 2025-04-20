using UnityEngine;

namespace September.InGame
{
    public class Projectile : MonoBehaviour
    {
        [SerializeField] private float _lifeTime = 5f;

        private void Start()
        {
            Destroy(gameObject, _lifeTime);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                Debug.Log("痛い");
                // ToDo : ダメージ処理
            }
        }
    }
}