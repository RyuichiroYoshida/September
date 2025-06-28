using Cysharp.Threading.Tasks;
using Fusion;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.SceneManagement;

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
        NetworkRunner _networkRunner;
        UniTask _currentTask;
        private void Start()
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
        public void CreateLobby(string gameName, int playerCount)
        {
            if (!_currentTask.Status.IsCompleted()) return;
            _loadingIcon.StartAnimation();
            _currentTask = CreateLobbyAsync(gameName, playerCount).Preserve();
            _currentTask.GetAwaiter().OnCompleted(()=>_loadingIcon.StopAnimation());
        }
        async UniTask CreateLobbyAsync(string gameName, int playerCount)
        {
            var result = await _networkRunner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Host,
                SessionName = gameName,
                PlayerCount = playerCount
            });
            if (!result.Ok)
            {
                Debug.Log(result.ShutdownReason);
                await InitializeRunner();
                return;
            }
            await _networkRunner.SpawnAsync(_playerDatabasePrefab);
            await _networkRunner.LoadScene(_lobbySceneName);
        }
        public void JoinLobby(string gameName)
        {
            if (!_currentTask.Status.IsCompleted()) return;
            _loadingIcon.StartAnimation();
            _currentTask = JoinLobbyAsync(gameName).Preserve();
            _currentTask.GetAwaiter().OnCompleted(()=>_loadingIcon.StopAnimation());
        }
        async UniTask JoinLobbyAsync(string gameName)
        {
            var result = await _networkRunner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Client,
                SessionName = gameName,
            });
            if (!result.Ok)
            {
                Debug.Log(result.ShutdownReason);
                await InitializeRunner();
            }
        }
        async UniTask InitializeRunner()
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

        public async UniTaskVoid StartGame()
        {
            if (!_networkRunner.IsServer) return;
            _networkRunner.SessionInfo.IsOpen = false;
            
            await _networkRunner.LoadScene(_gameSceneName);
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
}