using Fusion;
using InGame.Interact;
using UnityEngine;

namespace September.InGame.NauticalChart
{
    /// <summary> 海図のインタラクション効果を制御するクラス </summary>
    public class NauticalChartInteractEffect : CharacterInteractEffectBase
    {
        [SerializeField] private NauticalChartInteractable _nauticalChartInteractable;

        private PlayerRef _interactPlayerRef;

        /// <summary> NauticalChartInteractEffectを複製して返す </summary>
        public override CharacterInteractEffectBase Clone()
        {
            return new NauticalChartInteractEffect
            {
                _nauticalChartInteractable = _nauticalChartInteractable
            };
        }

        public override void OnInteractStart(IInteractableContext context, InteractableBase target)
        {
            _interactPlayerRef = PlayerRef.FromEncoded(context.Interactor);
            _nauticalChartInteractable.RPC_OnInteractStart(_interactPlayerRef);
            _nauticalChartInteractable.FixedUpdateNetwork();
        }
    }
}
