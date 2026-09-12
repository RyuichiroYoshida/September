using Fusion;
using UnityEngine;

namespace September.InGame.Kraken
{
    public class KrakenFactory : NetworkBehaviour
    {
        [SerializeField] private Kraken _krakenPrefab;

        public Kraken CreateKraken(Vector3 position, Quaternion rotation)
        {
            var kraken = Runner.Spawn(_krakenPrefab, position, rotation);
            
            return kraken;
        }
    }
}
