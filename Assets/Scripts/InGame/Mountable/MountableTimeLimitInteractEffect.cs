using System;
using Fusion;
using InGame.Interact;
using September.Common;
using September.Common.Attribute;
using September.InGame.Common;
using UnityEngine;

namespace September.InGame.Mountable
{
    public class MountableTimeLimitInteractEffect : CharacterInteractEffectBase
    {
        [SerializeField, RequireInterface(typeof(IMountable))] private MonoBehaviour _targetMountable;
        [SerializeField] private float _duration;

        private IMountable _mountable;
        private InteractableBase _target;
        private NetworkRunner _runner;
        private PlayerRef _currentOwner;
        private TickTimer _tickTimer;

        public override void OnInteractStart(IInteractableContext context, InteractableBase target)
        {
            if (_mountable == null)
            {
                if (_targetMountable is not IMountable mountable)
                {
                    throw new InvalidOperationException($"Target must be a Mountable: {(target ? target.ToString() : "Null")}");
                }

                _mountable = mountable;
            }

            if (!StaticServiceLocator.Instance.TryGet(out InGameManager manager))
            {
                Debug.LogWarning($"[{nameof(MountableTimeLimitInteractEffect)}] In Game Manager not found", target);
                return;
            }

            _runner = manager.Runner;
            _tickTimer = TickTimer.CreateFromSeconds(_runner, _duration);

            _currentOwner = PlayerRef.FromEncoded(context.Interactor);
            _target = target;
        }

        public override void OnInteractFixedNetworkUpdate(PlayerInput playerInput)
        {
            if (!_runner) return;

            if (!_tickTimer.Expired(_runner)) return;

            _mountable.GetOff(_currentOwner);
            _target.EndInteract();
        }

        public override CharacterInteractEffectBase Clone()
        {
            return MemberwiseClone() as MountableTimeLimitInteractEffect;
        }
    }
}
