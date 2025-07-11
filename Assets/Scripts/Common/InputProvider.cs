using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace September.Common
{
    public enum PlayerButtons
    {
        Jump,
        Dash,
        Interact,
        Attack,
        Ability1,
        Ability2,
    }

    public struct PlayerInput : INetworkInput
    {
        public NetworkButtons Buttons;
        public Vector2 MoveDirection;
        public float CameraYaw;
        public Vector3 DesiredLookDirection;
    }
    /// <summary>
    /// ネットワークの入力管理クラス
    /// </summary>
    public class InputProvider : SimulationBehaviour, INetworkRunnerCallbacks
    {
        Camera _mainCamera;
        
        private void Awake()
        {
            // InputSystemを有効にする
            GameInput.I.Enable();
            
            _mainCamera = Camera.main;
        }
        
        /// <summary>
        /// 現在の入力状況をネットワークに登録する
        /// </summary>
        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            var playerInput = new PlayerInput();
            var playerActions = GameInput.I.Player;
            //  Input Actionからデータを取り出してネットワークに登録する
            playerInput.Buttons.Set(PlayerButtons.Jump, playerActions.Jump.IsPressed());
            playerInput.Buttons.Set(PlayerButtons.Dash, playerActions.Dash.IsPressed());
            playerInput.Buttons.Set(PlayerButtons.Interact, playerActions.Interact.IsPressed());
            playerInput.Buttons.Set(PlayerButtons.Attack, playerActions.Attack.IsPressed());
            playerInput.Buttons.Set(PlayerButtons.Ability1, playerActions.Ability1.IsPressed());
            playerInput.MoveDirection = playerActions.Move.ReadValue<Vector2>();
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
            input.Set(playerInput);
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