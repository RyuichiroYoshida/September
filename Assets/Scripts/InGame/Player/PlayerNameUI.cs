using Fusion;
using September.Common;
using TMPro;
using UnityEngine;

namespace InGame.Player
{
    public class PlayerNameUI : NetworkBehaviour
    {
        [SerializeField] private Canvas _root;
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private float _maxDistance = 30f;

        private Camera _camera;
        private PlayerRef _ownerRef;
        private bool _isMine;

        /// <summary>
        /// 擬態中に表示する名前の所有者。
        /// Noneの場合は通常どおり、このPrefabのInputAuthorityの名前を表示する。
        /// </summary>
        [Networked, OnChangedRender(nameof(OnDisplayNameOwnerChanged))]
        private PlayerRef DisplayNameOwner { get; set; } = PlayerRef.None;

        public override void Spawned()
        {
            _camera = Camera.main;
            _ownerRef = Object.InputAuthority;
            _isMine = _ownerRef == Runner.LocalPlayer;

            if (_isMine)
            {
                _root.gameObject.SetActive(false);
                return;
            }

            TrySetName();
        }

        private void TrySetName()
        {
            if (PlayerDatabase.Instance == null) return;

            var nameOwner = DisplayNameOwner != PlayerRef.None
                ? DisplayNameOwner
                : _ownerRef;

            if (PlayerDatabase.Instance.PlayerDataDic
                .TryGet(nameOwner, out var data))
            {
                _nameText.text = data.DisplayNickName;
            }
        }

        /// <summary>
        /// 擬態後の頭上表示名を、擬態対象プレイヤーの名前へ切り替える。
        /// </summary>
        public void SetMimicDisplayNameOwner(PlayerRef targetPlayer)
        {
            if (!HasStateAuthority || targetPlayer == PlayerRef.None)
                return;

            DisplayNameOwner = targetPlayer;
            TrySetName();
        }

        private void OnDisplayNameOwnerChanged()
        {
            TrySetName();
        }

        private void LateUpdate()
        {
            if (_isMine) return;

            if (!_camera)
            {
                _camera = Camera.main;
                if (!_camera) return;
            }

            RotateToCamera();
        }

        private void RotateToCamera()
        {
            _root.transform.rotation =
                Quaternion.LookRotation(-_camera.transform.forward);
        }
    }
}
