using System;
using Cysharp.Threading.Tasks;
using Fusion;
using September.InGame.Fields;
using September.InGame.UI;
using UnityEngine;

namespace InGame.Player
{
    public class PlayerRespawn : NetworkBehaviour
    {
        [SerializeField] private float _coolTime;
        [SerializeField] private PlayerManager _playerManager;

        [Networked, OnChangedRender(nameof(OnOutFieldStateChanged))]
        private bool IsOutField { get; set; }

        public event Action OnOutFieldEvent;
        public event Action OnRevivalFieldEvent;

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;

            if (IsOutField) return;

            if (OutOfFieldArea.I == null) return;

            IsOutField = OutOfFieldArea.I.IsOutOfField(transform.position);
        }

        private void OnOutFieldStateChanged()
        {
            if (HasInputAuthority)
            {
                UIController.I.ShowOutFieldUI(IsOutField);
            }

            if (IsOutField)
            {
                OnOutFieldEvent?.Invoke();
                RespawnAsync().Forget();
            }
        }

        private async UniTaskVoid RespawnAsync()
        {
            _playerManager.RPC_SetInvisible(true);
            await UniTask.WaitForSeconds(_coolTime);
            OnRevivalFieldEvent?.Invoke();
            IsOutField = false;
            _playerManager.RPC_SetInvisible(false);
        }
    }
}
