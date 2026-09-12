using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Fusion;
using InGame.Player;
using September.Common;
using September.InGame.Common;
using September.InGame.UI;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace September.InGame.Tutorial
{
    [DefaultExecutionOrder(-10000)]
    public class TutorialSceneSetup : MonoBehaviour
    {
        [SerializeField] private TutorialManager _tutorialManager;
        [SerializeField] private NetworkPrefabRef _playerPrefab;
        [SerializeField] private NetworkPrefabRef _playerDatabasePrefab;
        [SerializeField] private Transform _spawnPoint;
        [SerializeField] private UnityEngine.UI.Image _fadeImage;
        [SerializeField] private ControlsUIGenerator _controlsUIGenerator;
        [SerializeField] private ControlDescriptionType _controlDescriptionType;
        [SerializeField] private UIController _uiController;
        [Header("シーンから直接再生するときの設定")]
        [SerializeField] private NetworkRunner _runnerPrefab;
        [SerializeField] private GameObject _runtimeSystems;
        [SerializeField] private string _titleSceneName = "Title";
        [Header("開始時のカメラ")]
        [SerializeField] private Camera _sceneCamera;
        [SerializeField] private Vector3 _initialCameraOffset = new(0f, 2.764443f, -4.880627f);
        [SerializeField] private Vector3 _initialCameraAngles = new(10f, 0f, 0f);

        private NetworkRunner _runner;
        private bool _ownsRunner;
        private bool _isExiting;
        private Tween _fadeTween;
        private CinemachineBrain _cameraBrain;

        public static NetworkProjectConfig CreateRunnerConfig()
        {
            var global = NetworkProjectConfig.Global;
            var config = new NetworkProjectConfig();

            var fields = typeof(NetworkProjectConfig).GetFields(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            foreach (var field in fields)
            {
                field.SetValue(config, field.GetValue(global));
            }

            config.PeerMode = NetworkProjectConfig.PeerModes.Single;
            return config;
        }

        private void Awake()
        {
            // Title経由なら起動済みRunnerがあるため、シーンのネットワーク登録前に有効化する。
            _runner = NetworkRunner.GetRunnerForScene(gameObject.scene);
            if (!_runner && NetworkManager.Instance) _runner = NetworkManager.Instance.Runner;
            if (_runner && _runner.IsRunning && _runtimeSystems) _runtimeSystems.SetActive(true);
            // プレイヤーが生成されるまでは、開始地点から見た初期カメラ位置を表示する。
            if (_sceneCamera && _spawnPoint)
            {
                _sceneCamera.transform.SetPositionAndRotation(
                    _spawnPoint.position + _spawnPoint.rotation * _initialCameraOffset,
                    _spawnPoint.rotation * Quaternion.Euler(_initialCameraAngles));
                _cameraBrain = _sceneCamera.GetComponent<CinemachineBrain>();
                if (_cameraBrain) _cameraBrain.enabled = false;
            }
            if (_fadeImage)
            {
                _fadeImage.gameObject.SetActive(true);
                _fadeImage.color = Color.black;
                _fadeImage.raycastTarget = true;
            }
        }

        private void Start()
        {
            // シーン内のAwakeが完了してから、起動処理を順番に実行する。
            InitializeAsync().Forget();
        }

        private async UniTask InitializeAsync()
        {
            var token = this.GetCancellationTokenOnDestroy();
            try
            {
                if (!_tutorialManager || !_spawnPoint || !_uiController || !_controlsUIGenerator || !_runtimeSystems)
                    throw new InvalidOperationException("TutorialSceneSetupの必須参照が設定されていません。");

                GameInput.I.IsInputBlockedByUI = true;

                // Title経由の場合は、そのセッションのRunnerを使う。
                _runner = NetworkRunner.GetRunnerForScene(gameObject.scene);
                if (!_runner && NetworkManager.Instance)
                    _runner = NetworkManager.Instance.Runner;

                if (!_runner)
                {
                    if (!_runnerPrefab)
                        throw new InvalidOperationException("単体起動用のRunner Prefabが設定されていません。");
                    if (gameObject.scene.buildIndex < 0)
                        throw new InvalidOperationException("TutorialをBuild ProfilesのScene Listに登録してください。");

                    // 入力供給とFusionの物理設定を持つ共通Prefabを使用する。
                    _runner = Instantiate(_runnerPrefab);
                    _ownsRunner = true;
                    var sceneManager = _runner.GetComponent<NetworkSceneManagerDefault>();
                    if (!sceneManager) sceneManager = _runner.gameObject.AddComponent<NetworkSceneManagerDefault>();
                    sceneManager.IsSceneTakeOverEnabled = true;
                    _runner.ProvideInput = true;
                    var result = await _runner.StartGame(new StartGameArgs
                    {
                        GameMode = GameMode.Single,
                        Config = CreateRunnerConfig(),
                        SceneManager = sceneManager
                    });
                    token.ThrowIfCancellationRequested();
                    if (!result.Ok)
                        throw new InvalidOperationException($"Tutorialの起動に失敗しました: {result.ErrorMessage}");
                    // SpawnerのAwakeがIsServerを調べるため、Runnerの起動完了後に有効化する。
                    _runtimeSystems.SetActive(true);
                    // 共通システムのStartでInGameManagerが登録されてから、展示物のSpawnedを呼ぶ。
                    // 有効化直後はAwakeしか完了しておらず、乗り物が参照するサービスがまだない。
                    await UniTask.NextFrame(cancellationToken: token);
                    var sceneInGameManager = _runtimeSystems.GetComponentInChildren<InGameManager>();
                    if (!sceneInGameManager ||
                        !StaticServiceLocator.Instance.TryGet<InGameManager>(out var registeredManager) ||
                        registeredManager != sceneInGameManager)
                        throw new InvalidOperationException("TutorialのInGameManagerの登録が完了していません。");
                    // 有効化済みのシーンをFusionに引き継ぎ、NetworkObjectを登録する。
                    await _runner.LoadScene(SceneRef.FromIndex(gameObject.scene.buildIndex));
                    token.ThrowIfCancellationRequested();
                }

                // SceneのNetworkObject登録完了を待ってからネットワークの値を扱う。
                using var ready = CancellationTokenSource.CreateLinkedTokenSource(token);
                // usingは宣言と逆順に破棄されるため、CTSより先にタイマーを停止する。
                using var readyTimeout = ready.CancelAfterSlim(TimeSpan.FromSeconds(30));
                await UniTask.WaitUntil(() => _runner && _runner.IsRunning &&
                    _tutorialManager.Object && _tutorialManager.Object.IsValid, cancellationToken: ready.Token);
                if (_runner.GameMode != GameMode.Single)
                    throw new InvalidOperationException("Tutorialは1人用セッションで開始してください。");
                _runner.ProvideInput = true;
                // 共通システムのStartによるサービス登録も完了させてからUI・プレイヤーを準備する。
                await UniTask.NextFrame(cancellationToken: ready.Token);
                _uiController.SetUpStartUI();
                _controlsUIGenerator.GenerateDescription(_controlDescriptionType);
                await SpawnPlayerAsync(ready.Token);
                Debug.Log("[Tutorial] 初期化完了。チュートリアルを開始します。");
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // シーンを離れた場合は、そのシーンの初期化を続けない。
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Tutorial] 初期化を中断しました: {exception.Message}", this);
                if (_fadeImage)
                {
                    _fadeTween?.Kill();
                    _fadeImage.color = Color.clear;
                    _fadeImage.raycastTarget = false;
                }
                GameInput.I.IsInputBlockedByUI = false;
                CursorStateManager.ShowCursor();
                await ShutdownOwnedRunnerAsync();
            }
        }

        private async UniTask SpawnPlayerAsync(CancellationToken token)
        {
            var player = _runner.LocalPlayer;
            var database = PlayerDatabase.Instance;
            // このRunnerに所属するDBだけを再利用し、二重生成を防ぐ。
            if (!database || !database.Object || !database.Object.IsValid || database.Runner != _runner)
            {
                if (database)
                    throw new InvalidOperationException("別のセッションのPlayerDatabaseが残っています。");
                var databaseObject = await _runner.SpawnAsync(_playerDatabasePrefab);
                token.ThrowIfCancellationRequested();
                database = databaseObject.GetComponent<PlayerDatabase>();
            }
            await UniTask.WaitUntil(() => database && database.Object && database.Object.IsValid,
                cancellationToken: token);
            if (!database.PlayerDataDic.ContainsKey(player)) database.AddPlayerData(player);
            await UniTask.WaitUntil(() => database.PlayerDataDic.ContainsKey(player), cancellationToken: token);

            if (!_runner.TryGetPlayerObject(player, out var playerObject))
            {
                playerObject = await _runner.SpawnAsync(_playerPrefab, _spawnPoint.position, _spawnPoint.rotation, player);
                token.ThrowIfCancellationRequested();
                _runner.SetPlayerObject(player, playerObject);
            }
            var inGameManager = StaticServiceLocator.Instance.Get<InGameManager>();
            if (inGameManager != null && !inGameManager.PlayerDataDic.ContainsKey(player))
                inGameManager.AddPlayerObject(player, playerObject);

            var input = playerObject.GetComponent<PlayerInputManager>();
            if (!input) throw new InvalidOperationException("プレイヤーにPlayerInputManagerがありません。");
            await AlignPlayerCameraAsync(playerObject, token);
            if (_fadeImage)
            {
                _fadeImage.color = Color.black;
                _fadeTween = _fadeImage.DOFade(0f, 1f).SetEase(Ease.InOutQuad);
                await _fadeTween.ToUniTask(cancellationToken: token);
                _fadeImage.raycastTarget = false;
            }
            // 1人用の入力許可を復元する。説明パネルによる入力制限はTutorialManagerが管理する。
            GameInput.I.ToggleMoveInput(true);
            GameInput.I.ToggleActionInput(true);
            GameInput.I.ToggleLookInput(true);
            _tutorialManager.OnTutorialStart(playerObject, input);
        }

        private async UniTask AlignPlayerCameraAsync(NetworkObject playerObject, CancellationToken token)
        {
            var cameraController = playerObject.GetComponent<CameraController>();
            if (!_sceneCamera || !cameraController)
                throw new InvalidOperationException("Tutorialのカメラ参照が設定されていません。");

            // 初回だけブレンドを切り、生成前の視点から移動する映像を見せない。
            _sceneCamera.transform.SetPositionAndRotation(cameraController.GetCameraPosition(),
                Quaternion.LookRotation(cameraController.GetCameraForward(), Vector3.up));
            if (!_cameraBrain) return;
            var previousBlend = _cameraBrain.DefaultBlend;
            try
            {
                _cameraBrain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
                _cameraBrain.enabled = true;
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, token);
            }
            finally
            {
                if (_cameraBrain) _cameraBrain.DefaultBlend = previousBlend;
            }
        }

        public async UniTask ExitToTitleAsync()
        {
            if (_isExiting) return;
            _isExiting = true;
            if (!_ownsRunner && NetworkManager.Instance)
            {
                NetworkManager.Instance.QuitLobby().Forget();
                return;
            }
            // 単体起動したセッションを終了してからTitleを開き、通常の起動経路に戻す。
            if (!Application.CanStreamedLevelBeLoaded(_titleSceneName))
            {
                _isExiting = false;
                Debug.LogError($"TitleシーンがScene Listにありません: {_titleSceneName}", this);
                return;
            }
            await ShutdownOwnedRunnerAsync();
            GameInput.I.IsInputBlockedByUI = false;
            await SceneManager.LoadSceneAsync(_titleSceneName);
        }

        private async UniTask ShutdownOwnedRunnerAsync()
        {
            if (!_ownsRunner || !_runner) return;
            _ownsRunner = false;
            await _runner.Shutdown();
        }

        private void OnDestroy()
        {
            _fadeTween?.Kill();
            // 自分で作ったRunnerのみ終了する。Titleから渡されたRunnerはNetworkManagerが管理する。
            if (Application.isPlaying) ShutdownOwnedRunnerAsync().Forget();
        }
    }
}
