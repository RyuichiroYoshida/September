using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

namespace September.Common
{
    enum PlayerButtons
    {
        Jump,
        Interact,
        Attack
    }

    public struct PlayerInput : INetworkInput
    {
        public NetworkButtons Buttons;
        public Vector2 MoveDirection;
        public float CameraYaw;
    }
    /// <summary>
    /// ネットワークの入力管理クラス
    /// </summary>
    public class InputProvider : SimulationBehaviour, INetworkRunnerCallbacks
    {
        GameInput _playerInput;
        Camera _mainCamera;
        
        private void Awake()
        {
            // InputSystemを有効にする
            _playerInput = new GameInput();
            _playerInput.Enable();
            _playerInput.Player.Enable();
            
            _mainCamera = Camera.main;
        }
        
        /// <summary>
        /// 現在の入力状況をネットワークに登録する
        /// </summary>
        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            var playerInput = new PlayerInput();
            var playerActions = _playerInput.Player;
            //  Input Actionからデータを取り出してネットワークに登録する
            playerInput.Buttons.Set(PlayerButtons.Jump, playerActions.Jump.IsPressed());
            playerInput.Buttons.Set(PlayerButtons.Interact, playerActions.Interact.IsPressed());
            playerInput.Buttons.Set(PlayerButtons.Attack, playerActions.Attack.IsPressed());
            playerInput.MoveDirection = playerActions.Move.ReadValue<Vector2>();
            playerInput.CameraYaw = _mainCamera.transform.rotation.eulerAngles.y;
            input.Set(playerInput);
        }
        
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
    }
}