#if UNITY_EDITOR
using Cysharp.Threading.Tasks;
using September.Common;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace September.Editor.InGameDebug
{
	public class InGameDebugWindow : EditorWindow
	{
		private const string TitleScenePath = "Assets/Scenes/NetworkMock/Title.unity";
		private const int MaxPlayerCount = 4;
		private InGameDebugLobbyData _lobbyData = new();
		private Vector2 _scroll;

		private void OnEnable()
		{
			_lobbyData = InGameDebugDataRepository.TestLobbyData;
			InGameDebugDataRepository.OnViewUpdated += Repaint;
		}

		private void OnDisable()
		{
			if (!EditorApplication.isPlayingOrWillChangePlaymode) _lobbyData.IsStartedFromExtensionWindow = false;

			InGameDebugDataRepository.SaveLobbyData();
			InGameDebugDataRepository.OnViewUpdated -= Repaint;
		}

		[MenuItem("September/In Game Debug")]
		public static void Open()
		{
			GetWindow<InGameDebugWindow>("In Game Debug");
		}

		private void OnGUI()
		{
			if (Application.isPlaying && !_lobbyData.IsStartedFromExtensionWindow)
			{
				GUI.color = Color.red;
				EditorGUILayout.LabelField("このウィンドウ以外から再生が開始されました。\nこのウィンドウは無効化されています。", EditorStyles.wordWrappedLabel);
				GUI.color = Color.white;
			}

			GUI.enabled = !Application.isPlaying;
			_lobbyData.MapType = (MapType)EditorGUILayout.EnumPopup("Map Type", _lobbyData.MapType);
			_lobbyData.LobbyName = EditorGUILayout.TextField("Lobby Name", _lobbyData.LobbyName);
			_lobbyData.Nickname = EditorGUILayout.TextField("Nickname", _lobbyData.Nickname);

			EditorGUILayout.BeginVertical("box");
			EditorGUILayout.LabelField("Time Settings", EditorStyles.boldLabel);
			_lobbyData.TimeSettings.PreStartTime = Mathf.Max(0, EditorGUILayout.IntField(
				"Pre Start Time", _lobbyData.TimeSettings.PreStartTime));
			_lobbyData.TimeSettings.GameTime = Mathf.Max(0f, EditorGUILayout.FloatField(
				"Game Time", _lobbyData.TimeSettings.GameTime));
			EditorGUILayout.EndVertical();

			_scroll = EditorGUILayout.BeginScrollView(_scroll);

			for (var i = 0; i < _lobbyData.PlayerData.Count; i++)
			{
				EditorGUILayout.BeginHorizontal("box");
				EditorGUILayout.LabelField(_lobbyData.Nickname + (i == 0 ? "" : "_" + i));

				_lobbyData.PlayerData[i].CharacterType =
					(CharacterType)EditorGUILayout.EnumPopup(_lobbyData.PlayerData[i].CharacterType);

				if (Application.isPlaying)
				{
					GUI.enabled = false;
					EditorGUILayout.Toggle(_lobbyData.PlayerData[i].IsConnected);
					GUI.enabled = !Application.isPlaying;
				}

				if (GUILayout.Button("削除", GUILayout.Width(60))) _lobbyData.PlayerData.RemoveAt(i);
				EditorGUILayout.EndHorizontal();
			}

			GUI.enabled = true;

			EditorGUILayout.EndScrollView();

			GUILayout.Space(10);
			GUI.enabled = Application.isPlaying && _lobbyData.IsStartedFromExtensionWindow &&
			              !_lobbyData.RequestMoveToGameScene;
			GUI.backgroundColor = Color.green;
			if (GUILayout.Button("ゲーム開始"))
				_lobbyData.RequestMoveToGameScene = true;
			GUI.backgroundColor = Color.white;

			GUI.enabled = !Application.isPlaying && _lobbyData.PlayerData.Count < MaxPlayerCount;
			if (GUILayout.Button("追加"))
			{
				_lobbyData.PlayerData.Add(new InGameDebugLobbyData.PlayerSetupData());
			}
			GUI.enabled = !Application.isPlaying;

			GUI.enabled = _lobbyData.PlayerData.Count > 0;
			if (GUILayout.Button("デバッグ再生開始")) Run().Forget();
			GUI.enabled = true;
		}

		private bool TryLoadTitleScene()
		{
			if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
			{
				EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);
				return true;
			}

			return false;
		}

		private async UniTask Run()
		{
			if (!TryLoadTitleScene())
			{
				Debug.LogWarning("デバッグ再生がキャンセルされました。");
				return;
			}

			await UniTask.WaitForSeconds(1);
			if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
			{
				Debug.LogWarning("すでに実行中です。");
				return;
			}

			SessionState.SetBool("InGameDebug.IsMainEditor", true);
			// ゲーム開始時の設定データをJsonに書き出す(DomainReload対策)
			_lobbyData.IsStartedFromExtensionWindow = true;
			InGameDebugDataRepository.SaveLobbyData();
			// この後はDomainReloadが入るため、データのリセットが入る
			EditorApplication.isPlaying = true;
		}
	}
}
#endif
