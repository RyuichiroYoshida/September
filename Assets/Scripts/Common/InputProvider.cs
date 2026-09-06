using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

namespace September.Common
{
    public enum PlayerButtons
    {
        Jump,
        Dash,
        Interact,
        Attack,
        Aim,
        Ability1,
        Ability2,
        Ability3,
        Ultimate,
        Warp,
        AirplaneForward,
        AirPlaneBack,
        Shooting,
        Evasion
    }

    public struct PlayerInput : INetworkInput
    {
        public NetworkButtons Buttons;
        public Vector2 MoveDirection;
        public Vector2 LookDirection;
        public float CameraYaw;
        public Vector3 DesiredLookDirection;
        public Vector3 CameraPosition;
    }
    /// <summary>
    /// ネットワークの入力管理クラス
    /// </summary>
    public class InputProvider : SimulationBehaviour, INetworkRunnerCallbacks
    {
        [Header("自動移動設定")]
        [SerializeField] private bool _useAutoMove = false;
        [SerializeField] private float _moveInterval = 2f;
        [SerializeField] private float _autoMoveTimer = 0f;
        [SerializeField] private int _moveDirection = 1;
        
        Camera _mainCamera;
        
        public bool UseAutoMove
        {
            get => _useAutoMove;
            set => _useAutoMove = value;
        }
        
        private void Awake()
        {
            // InputSystemを有効にする
            // GameInput.I.Enable();
            
            _mainCamera = Camera.main;
        }
        

        /// <summary>
        /// 現在の入力状況をネットワークに登録する
        /// </summary>
        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            var playerInput = new PlayerInput();
            if (_useAutoMove)
            {
                SetAutoMove(ref playerInput);
            }
            else
            {
                var playerActions = GameInput.I.Player;
                //  Input Actionからデータを取り出してネットワークに登録する（有効化されている場合のみ）

                // 移動関連の入力（Move、Jump、Dash、Aim、Evasion）
                if (playerActions.Move.enabled)
                {
                    playerInput.MoveDirection = playerActions.Move.ReadValue<Vector2>();
                    playerInput.LookDirection = playerActions.Look.ReadValue<Vector2>();
                    playerInput.Buttons.Set(PlayerButtons.Jump, playerActions.Jump.IsPressed());
                    playerInput.Buttons.Set(PlayerButtons.Dash, true); // 常にダッシュ（仮）
                    playerInput.Buttons.Set(PlayerButtons.Aim, playerActions.Aim.IsPressed());
                    playerInput.Buttons.Set(PlayerButtons.Evasion, playerActions.Dash.IsPressed());
                }
                else
                {
                    playerInput.MoveDirection = Vector2.zero;
                    playerInput.LookDirection = Vector2.zero;
                    playerInput.Buttons.Set(PlayerButtons.Jump, false);
                    playerInput.Buttons.Set(PlayerButtons.Dash, false);
                    playerInput.Buttons.Set(PlayerButtons.Aim, false);
                    playerInput.Buttons.Set(PlayerButtons.Evasion, false);
                }

                // アクション関連の入力
                if (playerActions.Attack.enabled)
                {
                    playerInput.Buttons.Set(PlayerButtons.Attack, playerActions.Attack.IsPressed());
                    playerInput.Buttons.Set(PlayerButtons.Ability1, playerActions.Ability1.IsPressed());
                    playerInput.Buttons.Set(PlayerButtons.Ability2, playerActions.Ability2.IsPressed());
                    playerInput.Buttons.Set(PlayerButtons.Ability3, playerActions.Ability3.IsPressed());
                    playerInput.Buttons.Set(PlayerButtons.Interact, playerActions.Interact.IsPressed());
                    playerInput.Buttons.Set(PlayerButtons.Shooting, playerActions.Shooting.IsPressed());
                }
                else
                {
                    playerInput.Buttons.Set(PlayerButtons.Attack, false);
                    playerInput.Buttons.Set(PlayerButtons.Ability1, false);
                    playerInput.Buttons.Set(PlayerButtons.Ability2, false);
                    playerInput.Buttons.Set(PlayerButtons.Ability3, false);
                    playerInput.Buttons.Set(PlayerButtons.Interact, false);
                    playerInput.Buttons.Set(PlayerButtons.Shooting, false);
                }

                // その他の入力（常に有効）
                playerInput.Buttons.Set(PlayerButtons.Warp, playerActions.Warp.IsPressed());
                playerInput.Buttons.Set(PlayerButtons.AirplaneForward, playerActions.AirplaneForward.IsPressed());
                playerInput.Buttons.Set(PlayerButtons.AirPlaneBack, playerActions.AirPlaneBack.IsPressed());
                playerInput.Buttons.Set(PlayerButtons.Ultimate, playerActions.Ultimate.IsPressed());
            }
            
            if (_mainCamera == null)
            {
                _mainCamera = Camera.main;
                if (_mainCamera == null)
                {
                    Debug.LogError("Main Cameraが見つかりません。カメラをシーンに配置してください。");
                    return;
                }
            }
            playerInput.CameraYaw = _mainCamera.transform.rotation.eulerAngles.y;
            Vector3 cameraForward = _mainCamera.transform.forward;
            playerInput.DesiredLookDirection = cameraForward.normalized;

            playerInput.CameraPosition = _mainCamera.transform.position;

            input.Set(playerInput);
        }

        private void SetAutoMove(ref PlayerInput playerInput)
        {
            // 自動移動の処理
            _autoMoveTimer += Time.deltaTime;
            if (_autoMoveTimer >= _moveInterval)
            {
                _moveDirection *= -1; // 移動方向を反転
                _autoMoveTimer = 0f; // タイマーをリセット
            }
                
            playerInput.MoveDirection = new Vector2(_moveDirection, 0f);
            playerInput.CameraYaw = 0f; // カメラのYawは固定
            playerInput.DesiredLookDirection = transform.forward; // 前方を向く
            playerInput.Buttons.Set(PlayerButtons.Dash, true);
        }

        #region CallbackEvents
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }

        public void OnSceneLoadDone(NetworkRunner runner)
        {
            _mainCamera = Camera.main;
        }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        #endregion
    }
}
