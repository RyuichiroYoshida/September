using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

namespace September.Common
{
    enum MyButtons
    {
        Jump,
        Interact
    }

    public struct MyInput : INetworkInput
    {
        public NetworkButtons Buttons;
        public Vector2 MoveDirection;
        public Vector2 LookDirection;   //  同期する意味ない
    }
    public class InputProvider : SimulationBehaviour, INetworkRunnerCallbacks
    {
        PlayerInput _playerInput;
        Camera _camera;
        private void Awake()
        {
            _playerInput = new PlayerInput();
            
            _playerInput.Enable();
            _playerInput.Player.Enable();
            var runner = NetworkRunner.GetRunnerForGameObject(gameObject);
            if (runner != null && runner.IsRunning)
            {
                runner.AddCallbacks(this);
                runner.AddGlobal(this);
            }
        }

        public void OnDestroy()
        {
            _playerInput.Disable();
            _playerInput.Player.Disable();
            var runner = NetworkRunner.GetRunnerForGameObject(gameObject);
            if (runner != null && runner.IsRunning)
            {
                runner.RemoveCallbacks(this);
                runner.RemoveGlobal(this);
            }
        }
        /// <summary>
        /// 現在の入力状況をネットワークに登録する
        /// </summary>
        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            var myInput = new MyInput();
            var playerActions = _playerInput.Player;
            myInput.Buttons.Set(MyButtons.Jump, playerActions.Jump.IsPressed());
            myInput.Buttons.Set(MyButtons.Interact, playerActions.Interact.IsPressed());
            if (_camera)
            {
                var moveDir = playerActions.Move.ReadValue<Vector2>();
                var cameraRotation = Quaternion.Euler(0f, _camera.transform.rotation.eulerAngles.y, 0f);
                var dir = cameraRotation * new Vector3(moveDir.x, 0f, moveDir.y);
                myInput.MoveDirection = new Vector2(dir.x, dir.z);
            }
            else
            {
                myInput.MoveDirection = playerActions.Move.ReadValue<Vector2>();
            }
            myInput.LookDirection = playerActions.Look.ReadValue<Vector2>();
            input.Set(myInput);
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
            _camera = Camera.main;
        }
        public void OnSceneLoadStart(NetworkRunner runner) { }
    }
}