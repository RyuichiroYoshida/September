using System;
using System.Linq;
using CRISound;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Fusion;
using GameEvent;
using InGame.Player;
using NaughtyAttributes;
using September.InGame.Common;
using September.InGame.Common.Stats;
using September.InGame.Performances;
using September.InGame.Rules;
using September.InGame.UI;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace September.Common
{
    public partial class PreparationState : ImtStateMachine<InGameManager>.State
    {
        [SerializeField] private Transform[] _spawnPositions;
        [SerializeField] private Image _fadeImage;
        [SerializeField] private CinemachineVirtualCamera _startCamera;
        [SerializeField] private Vector3 _cameraOffset;
        [SerializeField] private SetIcon _setIcon;
        [SerializeField, Scene] private string[] _additiveLoadScenes;
        [SerializeField, Tooltip("開始時のPlayerの位置をランダム化する")] private bool _isRandomSpawn = false;
        [SerializeReference, SubclassSelector] private IGameStartPerformance[] _gameStartPerformances;
        private bool _hasRecordedPlayerSelection = false;
        public bool IsRandomSpawn { get => _isRandomSpawn; set => _isRandomSpawn = value; }

        private IGameStartStrategy GameStartStrategy => Context.GameRule.GameStartStrategy;
        private IPlayerKilledStrategy PlayerKilledStrategy => Context.GameRule.PlayerKilledStrategy;

        private PlayerKillUseCase _playerKillUseCase;

        protected internal override void OnEnter()
        {
            BGMManager.StopBGM();

            if (_fadeImage) _fadeImage.gameObject.SetActive(true);
            HideCursor();
            UIController.I.SetUpStartUI();

            _playerKillUseCase = new PlayerKillUseCase(PlayerKilledStrategy);

            if (PlayerDatabase.Instance.PlayerDataDic.TryGet(Context.Runner.LocalPlayer, out SessionPlayerData localData))
            {
                ControlDescriptionType type = CharacterDataContainer.Instance.GetControlDescriptionType(localData.CharacterType);

                UIController.I.ChangeDescriptionUI(type);
            }

            if (Context.Runner.IsServer)
            {
                GameStartStrategy.OnGameStarted();
                Initialize().Forget();
                RPC_OpeningSequence();
            }
        }

        private async UniTask Initialize()
        {
            await UniTask.WhenAll(
                _additiveLoadScenes.Select(scene => Runner.LoadScene(scene, LoadSceneMode.Additive).ToUniTask())
            );
            await SpawnPlayers();

            // プレイヤーのキャラクター選択を一度だけ記録
            if (!_hasRecordedPlayerSelection)
            {
                _hasRecordedPlayerSelection = true;
                foreach (var pair in PlayerDatabase.Instance.PlayerDataDic)
                {
                    var data = new GameEventData();
                    data.DataPack("Player", pair.Key.ToString());
                    data.DataPack("Chara", pair.Value.CharacterType.ToString());
                    data.DataPack("Name", pair.Value.DisplayNickName);
                    GameEventRecorder.SendEventData("UserSelect", data);
                }
            }

            Context.Register(StaticServiceLocator.Instance);
        }

        private async UniTask SpawnPlayers()
        {
            var container = CharacterDataContainer.Instance;
            // キャラクターのスポーン位置をランダムに。
            var spawnPositions = _isRandomSpawn ? GetShuffledSpawnPoints() : _spawnPositions;
            // スポーンポイント設定が適用されていない場合はこのコンポーネント位置を暫定で初期値とする。
            if (spawnPositions == null || spawnPositions.Length == 0)
                spawnPositions = new[] { transform };
            var index = 0;
            foreach (var pair in PlayerDatabase.Instance.PlayerDataDic)
            {
                // ランダム化の際にPlayerのrotationを制御可能にするために、spawnPositionのrotationに合わせる。
                var spawnTransform = spawnPositions[index % spawnPositions.Length];
                var characterData = container.GetCharacterData(pair.Value.CharacterType);
                var prefab = pair.Key.AsIndex >= PlayerDatabase.BotStartIndex ? characterData.BotPrefab : characterData.Prefab;

                var player = await Context.Runner.SpawnAsync(
                    prefab,
                    spawnTransform.position,
                    spawnTransform.rotation,
                    inputAuthority: pair.Key);

                #region ビルドシステム

                var buildGenerator = player.GetComponentInChildren<BuildGenerator>();
                if (buildGenerator != null) buildGenerator.GenerateBuild(pair.Value.BuildType);
#if UNITY_EDITOR
                var root = transform.root;
                if (root != null)
                    Debug.Log($"{root.name} : ビルドシステムの構築に" +
                              (buildGenerator != null ? $"成功しました\n選択ビルド : {pair.Value.BuildType}" : "失敗しました"));
#endif

                #endregion

                PlayerDatabase.Instance.AddPlayerObject(pair.Key, player);

                Context.Runner.SetPlayerObject(pair.Key, player);

                if (!Context.PlayerDataDic.ContainsKey(pair.Key))
                {
                    Context.AddPlayerObject(pair.Key, player);
                }

                var playerHealth = player.GetComponent<PlayerHealth>();
                playerHealth.OnDeath += hitData =>
                {
                    PlayerRef killer = hitData.ExecutorRef;
                    PlayerRef victim = hitData.TargetRef;
                    Context.PlayerKilled?.Invoke(killer, victim);
                    _playerKillUseCase.Execute(hitData);
                };

                var spd = pair.Value;
                foreach (var playerData in PlayerDatabase.Instance.PlayerDataDic)
                {
                    if (pair.Key == playerData.Key) continue;
                    spd.StunData.Add(playerData.Key, 0);
                }

                PlayerDatabase.Instance.PlayerDataDic.Set(pair.Key, spd);
                _setIcon.ShowIcon(pair.Key);

                index++;
            }
        }

        private Transform[] GetShuffledSpawnPoints()
        {
            return _spawnPositions.OrderBy(_ => Random.value).ToArray();
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_OpeningSequence()
        {
            OpeningSequence().Forget();
        }

        private async UniTaskVoid OpeningSequence()
        {
            // 全クライアントで入力を無効化
            if (HasStateAuthority) RPC_ToggleInputs(false, false, false);
            _startCamera.Priority = 999;
            _startCamera.ForceCameraPosition(_spawnPositions[0].position + _cameraOffset, Quaternion.identity);
            //  黒画面フェードアウト
            await FadeOut();

            if (!_skipPreparationPerformances)
            {
                IGameStartPerformance.Context ctx = new()
                {
                    Runner = Runner,
                    ToggleInputs = RPC_ToggleInputs
                };

                foreach (IGameStartPerformance p in _gameStartPerformances)
                {
                    if (!p.Enabled) continue;
                    await p.RunPerformance(ctx);
                }
            }

            // 準備フェーズ開始 - 全クライアントで移動入力を有効化

            _startCamera.Priority = -999;

            BGMManager.ChangeBGM(SceneManager.GetActiveScene().name).Forget();

            // タイマー開始
            UIController.I.StartTimer(Context.Runner);

            var countDownDuration = StaticServiceLocator.Instance.Get<InGameManager>().TimerData.PreStartTime;
            if (countDownDuration > 0) // 準備フェーズの時間が0秒以下の場合は下記の処理をスキップする。
            {
                if (HasStateAuthority) RPC_ToggleInputs(true, false, true);

                // 準備フェーズを開始する
                UIController.I.TimeOverlayMessage?.Invoke(TimeMessageType.PreparationStart).Forget();
                // 準備フェーズが終了するまで待機
                await UniTask.Delay(TimeSpan.FromSeconds(countDownDuration));
            }

            //  ゲーム開始 - 全クライアントでアクション入力も有効化
            if (HasStateAuthority) RPC_ToggleInputs(true, true, true);
            //  ゲーム開始表示
            if (UIController.I.TimeOverlayMessage != null)
            {
                await UIController.I.TimeOverlayMessage.Invoke(TimeMessageType.GameStart);
            }

            if (Context.Runner.IsServer)
            {
                //  ステート終了
                Context.Rpc_SendEvent((int)StateEventId.Finish);
            }
        }

        // 全ての準備が整ったらFadeをあける
        private async UniTask FadeOut()
        {
            if (_fadeImage)
            {
                _fadeImage.color = new Color(0f, 0f, 0f, 1f);

                await _fadeImage.DOFade(0f, 1f).SetEase(Ease.InOutQuad);
            }
            else
            {
                await UniTask.Delay(TimeSpan.FromSeconds(1f));
            }
        }

        private void HideCursor()
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        /// <summary>
        /// 全クライアントの入力状態を切り替えるRPC
        /// </summary>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_ToggleInputs(bool moveInputEnabled, bool actionInputEnabled, bool lookInputEnabled)
        {
            GameInput.I.ToggleMoveInput(moveInputEnabled);
            GameInput.I.ToggleActionInput(actionInputEnabled);
            GameInput.I.ToggleLookInput(lookInputEnabled);
        }
    }
}
