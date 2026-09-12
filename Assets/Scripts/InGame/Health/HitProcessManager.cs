using InGame.Player;
using September.InGame.Jewelry;
using UnityEngine;

namespace September.InGame.Health
{
    public class HitProcessManager : MonoBehaviour
    {
        [SerializeField] private PlayerHealth _target;
        [SubclassSelector, SerializeReference] private IHitProcessor[] _hitProcessors;

        private void Start()
        {
            foreach (IHitProcessor processor in _hitProcessors)
            {
                _target.OnHitTaken += processor.OnHitTaken;
            }
        }

        private void OnDestroy()
        {
            foreach (IHitProcessor processor in _hitProcessors)
            {
                _target.OnHitTaken -= processor.OnHitTaken;
            }
        }
    }
}
