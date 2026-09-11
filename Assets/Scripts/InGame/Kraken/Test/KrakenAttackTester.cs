using NaughtyAttributes;
using UnityEngine;

namespace September.InGame.Kraken.Test
{
    public class KrakenAttackTester : MonoBehaviour
    {
        [SerializeField] private Kraken _kraken;
        [SerializeField] private Transform _target;

        [Button(enabledMode: EButtonEnableMode.Playmode)]
        public void RunTest()
        {
            _kraken.Attack(_target.position);
        }
    }
}
