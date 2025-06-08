using System;
using Fusion;
using September.Common;
using UnityEngine;

namespace InGame.Interact
{
    /// <summary>
    /// インタラクト可能なオブジェクトのインターフェース
    /// </summary>
    /// <remarks>
    /// このクラスは MonoBehaviour を継承しているため、Unity のコンポーネントとして使用できます。
    /// </remarks>
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public class PlayerInteractionController : NetworkBehaviour
    {
        [SerializeField] private float _interactRadius = 2.5f;
        [SerializeField] private LayerMask _interactMask;
        [SerializeField] private Transform _interactOrigin;

        private readonly Collider[] _hitBuffer = new Collider[16];
        private bool _wasInteractingLastFrame = false;

        private void Awake()
        {
            if (_interactOrigin == null)
            {
                _interactOrigin = transform; // デフォルトでは自身の位置を使用
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasInputAuthority) return;

            if (GetInput(out PlayerInput input))
            {
                bool isInteracting = input.Buttons.IsSet(PlayerButtons.Interact);

                // トリガー型の操作にしたい場合：押した瞬間だけ反応
                if (isInteracting && !_wasInteractingLastFrame)
                {
                    TryInteract();
                }

                _wasInteractingLastFrame = isInteracting;
            }
        }

        private void TryInteract()
        {
            int count = Physics.OverlapSphereNonAlloc(
                _interactOrigin.position,
                _interactRadius,
                _hitBuffer,
                _interactMask,
                QueryTriggerInteraction.Collide
            );

            for (int i = 0; i < count; i++)
            {
                var col = _hitBuffer[i];
                if (col.TryGetComponent(out IInteractable interactable))
                {
                    var context = new InteractableContext
                    {
                        Interactor = Object.InputAuthority.RawEncoded,
                        WorldPosition = _interactOrigin.position
                    };

                    // Hostであれば直接呼ぶ
                    if (HasStateAuthority)
                    {
                        interactable.Interact(context);
                    }
                    else
                    {
                        RPC_RequestInteract(Object.InputAuthority, col.GetComponent<NetworkObject>());
                    }

                    break; // 最初の対象だけ
                }
            }
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RPC_RequestInteract(PlayerRef interactor, NetworkObject target)
        {
            if (target != null && target.TryGetComponent(out IInteractable interactable))
            {
                var context = new InteractableContext
                {
                    Interactor = interactor.RawEncoded,
                    WorldPosition = transform.position
                };

                interactable.Interact(context);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (_interactOrigin != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(_interactOrigin.position, _interactRadius);
            }
        }
    }
}