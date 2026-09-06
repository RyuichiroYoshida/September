#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Fusion;
using Fusion.Sockets;
using September.Common;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;


namespace September.Editor.InGameDebug
{
	[InitializeOnLoad]
	public static class InGameDebugEntryPoint
	{
		static InGameDebugEntryPoint()
		{
			var lobbyData = InGameDebugDataRepository.TestLobbyData;
			if (lobbyData.IsStartedFromExtensionWindow)
			{
				EditorApplication.playModeStateChanged += StartGame;
			}
		}

		private static void StartGame(PlayModeStateChange mode)
		{
			var lobbyData = InGameDebugDataRepository.TestLobbyData;
			switch (mode)
			{
				case PlayModeStateChange.EnteredPlayMode:
					if (lobbyData == null || lobbyData.IsStartedFromExtensionWindow == false)
						return;
					var testRun = new InGameDebugRunner();
					testRun.RunAsync().Forget();
					break;

				case PlayModeStateChange.ExitingPlayMode:
					lobbyData.IsStartedFromExtensionWindow = false;
					InGameDebugTimeInjector.Clear();
					InGameDebugDataRepository.SaveLobbyData();
					break;
			}
		}
	}

	public class InGameDebugRunner : INetworkRunnerCallbacks
	{
		private readonly float _joinRetrySecond = 15f;
		private InGameDebugLobbyData _lobbyData;
		private NetworkRunner _networkRunner;
		private string GameName => _lobbyData.LobbyName;
		private int MaxPlayers => _lobbyData.PlayerData.Count;
		private List<InGameDebugLobbyData.PlayerSetupData> PlayersData => _lobbyData.PlayerData;

		public async UniTask RunAsync()
		{
			var networkManager = NetworkManager.Instance;
			_lobbyData = InGameDebugDataRepository.TestLobbyData;

			if (_lobbyData.PlayerData.Count == 0)
			{
				Debug.LogError("プレイヤーのデータがありません。\n" +
				               "少なくとも1人以上のキャラクターを作成してください。");
				return;
			}
			
			NickNameProvider.SetNickName(_lobbyData.Nickname);
			InGameDebugTimeInjector.Set(_lobbyData.TimeSettings);

			// これがメインエディターであればLobbyを作成する。
			if (SessionState.GetBool("InGameDebug.IsMainEditor", false))
				await networkManager.CreateLobby(GameName, MaxPlayers);
			else　// メインエディターでなければLobbyに入れる
				await JoinLobby();

			// NetworkRunnerをFindで取得する
			_networkRunner = Object.FindFirstObjectByType<NetworkRunner>();
			_networkRunner.AddCallbacks(this);
			UpdatePlayerConnectionState();

			await UniTask.WaitUntil(() => PlayerDatabase.Instance != null);
			var playerDatabase = PlayerDatabase.Instance;
			var localPlayerRef = _networkRunner.LocalPlayer;
			playerDatabase.AddPlayerData(localPlayerRef);

			if (!_networkRunner.IsServer) return;

			// 指定人数に達するか、Editor上でボタンが押されるまで待機
			await UniTask.WaitUntil(() => _lobbyData.RequestMoveToGameScene);
			await UniTask.Delay(1000);

			// 設定されたキャラクターを人数分、順番に割り振る
			var index = 0;
			foreach (var player in playerDatabase.PlayerDataDic)
				if (index < PlayersData.Count)
					playerDatabase.Rpc_SetCharacter(player.Key, PlayersData[index++].CharacterType);

			networkManager.StartGame(new GameStartContext(_lobbyData.MapType)).Forget();
		}

		private async UniTask JoinLobby()
		{
			for (var i = 0; i < 10; i++)
			{
				Debug.Log("Try to join lobby: " + GameName);
				var joinResult = await NetworkManager.Instance.JoinLobby(GameName);
				if (joinResult.Ok)
					return;
				switch (joinResult.ShutdownReason)
				{
					case ShutdownReason.ConnectionTimeout:
					case ShutdownReason.ConnectionRefused:
					case ShutdownReason.GameNotFound:
						await UniTask.WaitForSeconds(_joinRetrySecond);
						continue;
					default:
						Debug.LogError($"Failed to join lobby: {joinResult.ShutdownReason}");
						return;
				}
			}
		}

		private void UpdatePlayerConnectionState()
		{
			if (_networkRunner == null)
			{
				Debug.LogWarning("NetworkRunner is null. Cannot add player.");
				return;
			}

			if (!_networkRunner.IsServer) return;

			var ActivePlayerCount = _networkRunner.ActivePlayers.Count();
			var count = Mathf.Min(ActivePlayerCount, _lobbyData.PlayerData.Count);

			// アクティブプレイヤーの数だけ順番にConnectしたことにする。
			for (var i = 0; i < count; i++)
			{
				_lobbyData.PlayerData[i].IsConnected = true;
			}

			// プレイヤーが指定数集まっていればゲームを開始する。
			if (_lobbyData.PlayerData.Count <= ActivePlayerCount)
			{
				_lobbyData.RequestMoveToGameScene = true;
			}

			InGameDebugDataRepository.EditorUpdate();
		}

		void INetworkRunnerCallbacks.OnPlayerJoined(NetworkRunner runner, PlayerRef player)
		{
			UpdatePlayerConnectionState();
		}

		#region INetworkRunnerCallbacks

		void INetworkRunnerCallbacks.OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
		{
		}

		void INetworkRunnerCallbacks.OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
		{
		}

		void INetworkRunnerCallbacks.OnPlayerLeft(NetworkRunner runner, PlayerRef player)
		{
		}

		void INetworkRunnerCallbacks.OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
		{
		}

		void INetworkRunnerCallbacks.OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
		{
		}

		void INetworkRunnerCallbacks.OnConnectRequest(NetworkRunner runner,
			NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
		{
		}

		void INetworkRunnerCallbacks.OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress,
			NetConnectFailedReason reason)
		{
		}

		void INetworkRunnerCallbacks.OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message)
		{
		}

		void INetworkRunnerCallbacks.OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key,
			ArraySegment<byte> data)
		{
		}

		void INetworkRunnerCallbacks.OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key,
			float progress)
		{
		}

		void INetworkRunnerCallbacks.OnInput(NetworkRunner runner, NetworkInput input)
		{
		}

		void INetworkRunnerCallbacks.OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
		{
		}

		void INetworkRunnerCallbacks.OnConnectedToServer(NetworkRunner runner)
		{
		}

		void INetworkRunnerCallbacks.OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
		{
		}

		void INetworkRunnerCallbacks.OnCustomAuthenticationResponse(NetworkRunner runner,
			Dictionary<string, object> data)
		{
		}

		void INetworkRunnerCallbacks.OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
		{
		}

		void INetworkRunnerCallbacks.OnSceneLoadDone(NetworkRunner runner)
		{
		}

		void INetworkRunnerCallbacks.OnSceneLoadStart(NetworkRunner runner)
		{
		}

		#endregion
	}
}
#endif
