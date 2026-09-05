using Fusion;
using InGame.Common;
using InGame.Player;
using September.Common;
using September.InGame.UI;
using UnityEngine;

namespace Ingame.Tanihira
{
    public class TanihiraCursor : NetworkBehaviour, IAfterTick, IMimicCleanup
    {
        [Header("Cursor")]
        [SerializeField] private GameObject _cursorPrefab;
        [Header("Sensitivity")]
        [SerializeField] private float _sensitivity = 0.1f;
        [Header("Rayの設定")] 
        [SerializeField] private float _rayCastHeight;
        [SerializeField] private LayerMask _raycastMask;
        [SerializeField] private float _hitDistance;
        [Header("カメラ移動")]
        [SerializeField] private Vector3 _stanceCameraOffset;
        [SerializeField] private float _changeOffsetDuration;
        [Header("カーソルのオフセットY")]
        [SerializeField] private float _cursorOffsetY;
        
        private Transform _playerTransform;
        private GameObject _cursorObject;
        private Vector3 _cursorPosition;
        [SerializeField] private NetworkObject _moveTargetPrefab;
        private NetworkObject _moveTargetInstance;
        public Transform MoveTargetTransform => _moveTargetInstance.transform;
        
        private Camera _mainCamera;
        private PlayerManager _playerManager;
        private AnimationClipPlayer _clipPlayer;
        private PlayerMovement _movement;
        private CameraController _cameraController;
        private FriendOrder _friendOrder;
        private FormationManager _formationManager;
        private NetworkButtons PreviousButtons { get; set; }
        [Networked, HideInInspector] private TanihiraCursorState _state { get; set; }

        public override void Spawned()
        {
            if (HasStateAuthority)
            {
                _playerManager = GetComponent<PlayerManager>();
                _movement = GetComponent<PlayerMovement>();
                _friendOrder = GetComponent<FriendOrder>();
                _formationManager = GetComponent<FormationManager>();
                _moveTargetInstance = Runner.Spawn(_moveTargetPrefab, Vector3.zero, Quaternion.identity);
            }
            
            if (HasInputAuthority)
            {
                _mainCamera = Camera.main;
                _playerManager = GetComponent<PlayerManager>();
                _movement = GetComponent<PlayerMovement>();
                _cameraController = GetComponent<CameraController>();
                _formationManager = GetComponent<FormationManager>();
                
                _playerTransform = this.transform;
                //前方最大範囲にカーソルを出現
                _cursorObject = Instantiate(_cursorPrefab, _playerTransform.position, Quaternion.identity);
                _cursorObject.SetActive(false);
                _state = TanihiraCursorState.Idol;
                if(!_friendOrder)_friendOrder = GetComponent<FriendOrder>();
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!GetInput<PlayerInput>(out var input)) return;
            
            //押した時(カーソルモードでないとき）
            if (_state == TanihiraCursorState.Idol && input.Buttons.WasPressed(PreviousButtons, PlayerButtons.Ability2))
            {
                StartCursor();
            }
            
            //離した時
            if (input.Buttons.WasReleased(PreviousButtons, PlayerButtons.Ability2))
            {
                EndCursor();
            }
            else if (input.Buttons.WasPressed(PreviousButtons, PlayerButtons.Ability3) && _state == TanihiraCursorState.Active)
            {
                MoveTargetCursor();
            }
            
            if (_state == TanihiraCursorState.Active)
            {
                MoveCursor();
            }
        }

        private void StartCursor()
        {
            if (HasInputAuthority)
            {
                _cameraController.ChangeOffset(_stanceCameraOffset, _changeOffsetDuration);
                _cursorObject.SetActive(true);
            }

            if (!HasStateAuthority) return;
            
            _state = TanihiraCursorState.Active;
            _playerManager.SetControlState(PlayerManager.PlayerControlState.InputLocked);
            RPC_ChangeDescriptionUI(ControlDescriptionType.TanihiraAiming);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
        private void RPC_ChangeDescriptionUI(ControlDescriptionType mode)
        {
            UIController.I.ChangeDescriptionUI(mode);
        }

        private void MoveCursor()
        {
            if (HasInputAuthority)
            {
                //モード中に攻撃ボタンを押した時
                _cursorPosition = MoveCursorPos();
            }
        }

        private void MoveTargetCursor()
        {
            if (HasInputAuthority)
            {
                //見えないカーソルを移動させる
                RPC_OrderMoveFriend(_cursorPosition);
            }
        }
        
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RPC_OrderMoveFriend(Vector3 targetPos)
        {
            _moveTargetInstance.transform.position = targetPos;
            _friendOrder.ExecuteOrderMoveFriend();
            _friendOrder.AllResetAttackAmount();
            _formationManager.SetAllAttackOrdered(true);
        }

        private void EndCursor()
        {
            if (HasInputAuthority)
            {
                _cameraController.ResetOffset(_changeOffsetDuration);
                _cursorObject.SetActive(false);
            }

            if (!HasStateAuthority) return;
            _state = TanihiraCursorState.Idol;
            _playerManager.SetControlState(PlayerManager.PlayerControlState.Normal);
            RPC_ChangeDescriptionUI(ControlDescriptionType.Tanihira);
        }

        public void CleanupBeforeMimicDespawn()
        {
            CleanupLocalPresentation();

            if (!HasStateAuthority)
                return;

            _state = TanihiraCursorState.Idol;
            if (_playerManager)
                _playerManager.SetControlState(PlayerManager.PlayerControlState.Normal);

            if (_moveTargetInstance)
            {
                Runner.Despawn(_moveTargetInstance);
                _moveTargetInstance = null;
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            CleanupLocalPresentation();
        }

        private void CleanupLocalPresentation()
        {
            if (_cameraController)
                _cameraController.ResetOffset(_changeOffsetDuration);

            if (_cursorObject)
            {
                Destroy(_cursorObject);
                _cursorObject = null;
            }
        }

        private Vector3 MoveCursorPos()
        {
            RaycastHit hit;
            Vector3 rayCastOrigin = _mainCamera.transform.position;
            if (Physics.Raycast(rayCastOrigin, _mainCamera.transform.forward, out hit, _hitDistance, _raycastMask))
            {
                //カーソルを移動
                _cursorObject.transform.position = new Vector3(hit.point.x, hit.point.y + _cursorOffsetY, hit.point.z);
                return _cursorObject.transform.position;
            }
            
            //変更が無かったら前のデータを返す
            return _cursorPosition;
        }

        public enum TanihiraCursorState
        {
            Idol,
            Active,
        }

        public void AfterTick()
        {
            PreviousButtons = GetInput<PlayerInput>().GetValueOrDefault().Buttons;
        }
    }
}
