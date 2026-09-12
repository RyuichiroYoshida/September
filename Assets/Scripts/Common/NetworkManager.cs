using Cysharp.Threading.Tasks;
using DG.Tweening;
using Fusion;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace September.Common
{
    public class NetworkManager : MonoBehaviour
    {
        public static NetworkManager Instance;
        [SerializeField] NetworkRunner _runnerPrefab;
        [SerializeField] PlayerDatabase _playerDatabasePrefab;
        [SerializeField] LoadingIcon _loadingIcon;
        [SerializeField, Scene] string _titleSceneName;
        [SerializeField, Scene] string _lobbySceneName;
        [SerializeField, Scene] string _gameSceneName;
        [SerializeField, Scene] string _resultSceneName;
        [SerializeField, Scene] string _tutorialSceneName;
        NetworkRunner _networkRunner;
        public NetworkRunner Runner => _networkRunner;
        UniTask<StartGameResult> _currentTask;

        /// <summary>
        /// ホストがルーム作成時に選択したMap。
        /// </summary>
        public MapType SelectedMapType { get; private set; } = MapType.Pirate;

        private void Awake()
        {
            if (Instance == null)
            {
                _networkRunner = Instantiate(_runnerPrefab);
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public async UniTask<StartGameResult> CreateLobby(string gameName, int playerCount, MapType mapType)
        {
            if (!_currentTask.Status.IsCompleted())
                return default;

            SelectedMapType = mapType;
            _loadingIcon.StartAnimation();

            try
            {
                _currentTask = CreateLobbyAsync(gameName, playerCount).Preserve();
                return await _currentTask;
            }
            finally
            {
                _loadingIcon.StopAnimation();
            }
        }

        public UniTask<StartGameResult> CreateLobby(string gameName, int playerCount)
        {
            return CreateLobby(gameName, playerCount, SelectedMapType);
        }

        public async UniTask LoadLobbyScene()
        {
            await _networkRunner.LoadScene(_lobbySceneName);
        }

        async UniTask<StartGameResult> CreateLobbyAsync(string gameName, int playerCount)
        {
            var result = await _networkRunner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Host,
                SessionName = gameName,
                PlayerCount = playerCount
            });

            if (!result.Ok)
            {
                await InitializeRunner();
                return result;
            }
            await _networkRunner.SpawnAsync(_playerDatabasePrefab);

            return result;
        }

        public async UniTask<StartGameResult> JoinLobby(string gameName)
        {
            if (!_currentTask.Status.IsCompleted())
                return default;
            _loadingIcon.StartAnimation();

            try
            {
                _currentTask = JoinLobbyAsync(gameName).Preserve();
                return await _currentTask;

            }
            finally
            {
                _loadingIcon.StopAnimation();
            }

        }

        async UniTask<StartGameResult> JoinLobbyAsync(string gameName)
        {
            var result = await _networkRunner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Client,
                SessionName = gameName,
            });

            if (!result.Ok)
            {

                await InitializeRunner();
            }
            return result;
        }

        public async UniTask InitializeRunner()
        {
            await _networkRunner.Shutdown();
            _networkRunner = Instantiate(_runnerPrefab);
        }

        public async UniTaskVoid QuitLobby()
        {
            await _networkRunner.Shutdown();
            await SceneManager.LoadSceneAsync(_titleSceneName);
            _networkRunner = Instantiate(_runnerPrefab);
        }

        public async UniTaskVoid StartGame(GameStartContext startContext)
        {
            if (!_networkRunner.IsServer) return;
            _networkRunner.SessionInfo.IsOpen = false;

            if (MapSceneContainer.TryGetInstance(out var mapSceneContainer))
            {
                string gameSceneName = mapSceneContainer.GetMapSceneName(startContext.MapType);
                await _networkRunner.LoadScene(gameSceneName);
            }
            else
            {
                Debug.LogError("MapSceneContainer not found");
            }
        }

        public async UniTaskVoid LoadTutorialScene()
        {
            var sceneManager = _networkRunner.GetComponent<NetworkSceneManagerDefault>();
            if (!sceneManager) sceneManager = _networkRunner.gameObject.AddComponent<NetworkSceneManagerDefault>();
            var result = await _networkRunner.StartGame(new StartGameArgs()
            {
                GameMode = GameMode.Single,
                Config = September.InGame.Tutorial.TutorialSceneSetup.CreateRunnerConfig(),
                SceneManager = sceneManager,
                Scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex)
            });

            if (result.Ok)
            {
                await _networkRunner.LoadScene(_tutorialSceneName);
            }
            else
            {
                Debug.LogError($"[Tutorial] Failed to start network: {result.ErrorMessage}");
            }
        }

        public async UniTask Fade(Image fadeImage)
        {
            fadeImage.gameObject.SetActive(true);
            fadeImage.color = new Color(0f, 0f, 0f, 0f);

            await fadeImage.DOFade(1f, 0.5f).SetEase(Ease.InOutQuad);
        }

        public async UniTask QuitInGame()
        {
            if (!_networkRunner.IsServer) return;
            _networkRunner.SessionInfo.IsOpen = false;

            if (!SceneManager.GetSceneByName("Field").isLoaded)
            {
                return;
            }

            await _networkRunner.UnloadScene("Field");
            await _networkRunner.LoadScene(_resultSceneName);
        }
    }

    public struct GameStartContext
    {
        public MapType MapType;
        public GameStartContext(MapType mapType)
        {
            MapType = mapType;
        }
    }
}
