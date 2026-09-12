using System;
using Fusion;
using InGame.Interact;
using September.Common;
using UnityEngine;

namespace September.InGame.Exhibit
{
    [Serializable]
    public class ProjectileInteractEffect : CharacterInteractEffectBase
    {
        [SerializeField] private ProjectileInteractableBase _projectileInteractableBase;

        public ProjectileInteractEffect(ProjectileInteractableBase interactableBase)
        {
            _projectileInteractableBase = interactableBase;
        }

        public ProjectileInteractEffect()
        {
        }

        public override void OnInteractStart(IInteractableContext context, InteractableBase target)
        {
            var playerRef = PlayerRef.FromEncoded(context.Interactor);
            _projectileInteractableBase.InteractStart(playerRef);
        }

        public override CharacterInteractEffectBase Clone()
        {
            return new ProjectileInteractEffect(_projectileInteractableBase);
        }
    }
}