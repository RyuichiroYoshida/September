using Fusion;
using UnityEngine;

namespace September.InGame.Kraken
{
    /// <summary>
    /// クラーケンの出現イベント
    /// </summary>
    public class KrakenEventHandler : NetworkBehaviour
    {
        [SerializeField] private KrakenFactory _krakenFactory;
        [SerializeField] private Transform _krakenSpawnPoint;

        public bool StartEvent(out Kraken kraken)
        {
            if (!HasStateAuthority)
            {
                Debug.LogError("[KrakenEventHandler] StateAuthority以外から呼び出されました");
                kraken = null;
                return false;
            }
            
            kraken = _krakenFactory.CreateKraken(_krakenSpawnPoint.position, _krakenSpawnPoint.rotation);

            return true;
        }
    }
}