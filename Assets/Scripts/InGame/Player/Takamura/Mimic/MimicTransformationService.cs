using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Fusion;
using September.Common;
using September.InGame.Common;
using September.InGame.Common.Stats;
using September.InGame.Rules;
using UnityEngine;

namespace InGame.Player.Takamura.Mimic
{
    /// <summary>
    /// プレイヤーPrefabの交換と時間切れによる復帰を管理する。
    /// 交換元のPlayerと一緒に消えないようNetworkRunner上へ動的に生成される。
    /// </summary>
    public sealed class MimicTransformationService : MonoBehaviour
    {
        private readonly Queue<TransformRequest> _pendingRequests = new();
        private readonly Dictionary<PlayerRef, ActiveTransformation> _activeTransformations = new();
        private readonly HashSet<PlayerRef> _processingPlayers = new();

        private NetworkRunner _runner;

        private readonly struct TransformRequest
        {
            public readonly PlayerRef Player;
            public readonly CharacterType TargetCharacterType;
            public readonly float Duration;
            public readonly int ExecuteTick;

            public TransformRequest(
                PlayerRef player,
                CharacterType targetCharacterType,
                float duration,
                int executeTick)
            {
                Player = player;
                TargetCharacterType = targetCharacterType;
                Duration = duration;
                ExecuteTick = executeTick;
            }
        }

        private readonly struct ActiveTransformation
        {
            public readonly NetworkPrefabRef OriginalPrefab;
            public readonly float ExpireTime;

            public ActiveTransformation(NetworkPrefabRef originalPrefab, float expireTime)
            {
                OriginalPrefab = originalPrefab;
                ExpireTime = expireTime;
            }
        }

        /// <summary>
        /// 擬態の予約をする静的メソッド
        /// </summary>
        /// <param name="owner">プレイヤー</param>
        /// <param name="duration">擬態有効時間</param>
        public static void ReserveTransform(
            NetworkObject owner,
            CharacterType targetCharacterType,
            float duration)
        {
            if (!owner || !owner.HasStateAuthority || owner.Runner == null)
                return;

            // このクラスをRunnerに逃がすことで安全にオブジェクトの入れ替えを行う
            var service = owner.Runner.GetComponent<MimicTransformationService>();
            if (!service)
                service = owner.Runner.gameObject.AddComponent<MimicTransformationService>();

            service.Initialize(owner.Runner);
            service.Enqueue(owner.InputAuthority, targetCharacterType, duration);
        }

        private void Initialize(NetworkRunner runner)
        {
            _runner ??= runner;
        }

        /// <summary>
        /// 擬態の予約をコレクションに追加するメソッド
        /// </summary>
        /// <param name="player">プレイヤー</param>
        /// <param name="targetCharacterType">擬態するキャラクターの種類</param>
        /// <param name="duration">擬態有効時間</param>
        private void Enqueue(
            PlayerRef player,
            CharacterType targetCharacterType,
            float duration)
        {
            if (_activeTransformations.ContainsKey(player) || _processingPlayers.Contains(player))
                return;

            _pendingRequests.Enqueue(new TransformRequest(
                player,
                targetCharacterType,
                duration,
                _runner.Tick + 1)); // 次のTickに予約
        }

        private void Update()
        {
            if (_runner == null || !_runner.IsRunning || !_runner.IsServer)
                return;

            ProcessPendingRequests();
            ProcessExpiredTransformations();
        }

        /// <summary>
        /// 擬態リクエスト実行メソッド
        /// </summary>
        private void ProcessPendingRequests()
        {
            if (_pendingRequests.Count == 0)
                return;

            var request = _pendingRequests.Peek();
            if (_runner.Tick < request.ExecuteTick)
                return;

            // リクエストが指定するのTick以降になったら実行
            _pendingRequests.Dequeue();
            TransformAsync(request).Forget();
        }

        /// <summary>
        /// 擬態終了リクエスト実行メソッド
        /// </summary>
        private void ProcessExpiredTransformations()
        {
            if (_activeTransformations.Count == 0)
                return;

            var expiredPlayers = new List<PlayerRef>();
            foreach (var pair in _activeTransformations)
            {
                // 擬態終了条件を満たした場合はコレクションに登録
                if (_runner.SimulationTime >= pair.Value.ExpireTime
                    && !_processingPlayers.Contains(pair.Key))
                {
                    expiredPlayers.Add(pair.Key);
                }
            }

            // 終了条件を満たしたプレイヤーに対して順に擬態解除を実行
            foreach (var player in expiredPlayers)
                RestoreAsync(player, _activeTransformations[player]).Forget();
        }

        /// <summary>
        /// 擬態を実行するメソッド
        /// </summary>
        /// <param name="request">擬態予約の情報</param>
        /// <returns></returns>
        private async UniTaskVoid TransformAsync(TransformRequest request)
        {
            // 擬態関連の処理中の場合は無視
            if (!_processingPlayers.Add(request.Player))
                return;

            try
            {
                // 現在操作しているキャラクターを取得できなかったら終了
                if (!TryGetCurrentPlayer(request.Player, out var oldPlayer))
                    return;

                var container = CharacterDataContainer.Instance;
                if (!container)
                {
                    Debug.LogError("[Mimic] CharacterDataContainerが読み込まれていません。");
                    return;
                }

                // 擬態対象のプレハブ参照を取得
                var copiedPrefab = container.GetCharacterData(request.TargetCharacterType).Prefab;
                if (copiedPrefab == default)
                {
                    Debug.LogError(
                        $"[Mimic] {request.TargetCharacterType}のPrefabが未登録です。");

                    return;
                }

                if (!PlayerDatabase.Instance.PlayerDataDic.TryGet(request.Player, out var playerData))
                {
                    Debug.LogError($"[Mimic] {request.Player} のSessionPlayerDataが見つかりません。");
                    return;
                }

                // 自分自身のプレハブ参照を取得
                var originalPrefab = container.GetCharacterData(playerData.CharacterType).Prefab;
                // 現在のプレイヤーの情報を保存
                var snapshot = PlayerTransformationSnapshot.Capture(oldPlayer);

                // 擬態対象プレハブ参照をもとにオブジェクトを生成
                var newPlayer = await _runner.SpawnAsync(
                    copiedPrefab,
                    snapshot.Position,
                    snapshot.Rotation,
                    inputAuthority: request.Player);    // 入力権限を移動

                // 擬態前の情報を反映
                snapshot.ApplyTo(newPlayer);
                // 新しく生成したオブジェクトが正常に動作するようにする
                InitializeReplacementPlayer(request.Player, newPlayer);
                // 操作するキャラクターの参照を置き換える
                ReplacePlayerReferences(request.Player, newPlayer);
                // 擬態前のオブジェクトを削除
                _runner.Despawn(oldPlayer);

                // 操作主に対して擬態前プレハブと擬態有効時間を保存
                _activeTransformations[request.Player] = new ActiveTransformation(
                    originalPrefab,
                    _runner.SimulationTime + request.Duration);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                // 擬態関連の処理中フラグを解除
                _processingPlayers.Remove(request.Player);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="player"></param>
        /// <param name="transformation"></param>
        /// <returns></returns>
        private async UniTaskVoid RestoreAsync(PlayerRef player, ActiveTransformation transformation)
        {
            // 擬態関連の処理中の場合は無視
            if (!_processingPlayers.Add(player))
                return;

            try
            {
                if (!TryGetCurrentPlayer(player, out var copiedPlayer))
                    return;

                // 擬態解除前の情報を保存
                var snapshot = PlayerTransformationSnapshot.Capture(copiedPlayer);
                // 擬態前のオブジェクトを生成
                var restoredPlayer = await _runner.SpawnAsync(
                    transformation.OriginalPrefab,
                    snapshot.Position,
                    snapshot.Rotation,
                    inputAuthority: player);

                // 擬態解除前の情報を反映
                snapshot.ApplyTo(restoredPlayer);
                // 擬態解除した時のオブジェクトが正常に動作するようにする
                InitializeReplacementPlayer(player, restoredPlayer);
                // 操作キャラクターの参照を置き換える
                ReplacePlayerReferences(player, restoredPlayer);
                // 擬態解除前のオブジェクトを削除
                _runner.Despawn(copiedPlayer);
                // 擬態中の情報を削除
                _activeTransformations.Remove(player);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                // 擬態関連の処理中フラグを解除
                _processingPlayers.Remove(player);
            }
        }

        /// <summary>
        /// 現在操作しているキャラクターを取得する静的メソッド
        /// </summary>
        /// <param name="player">操作主</param>
        /// <param name="playerObject">現在操作しているキャラクター</param>
        /// <returns>取得できたか</returns>
        private static bool TryGetCurrentPlayer(PlayerRef player, out NetworkObject playerObject)
        {
            playerObject = null;
            return PlayerDatabase.Instance != null
                   && PlayerDatabase.Instance.PlayerObjectDic.TryGet(player, out playerObject)
                   && playerObject;
        }

        /// <summary>
        /// プレイヤーが操作するキャラクターの参照を置き換えるメソッド
        /// </summary>
        /// <param name="player">操作主</param>
        /// <param name="newPlayer">新しく操作するキャラクター</param>
        private void ReplacePlayerReferences(PlayerRef player, NetworkObject newPlayer)
        {
            PlayerDatabase.Instance.AddPlayerObject(player, newPlayer);
            _runner.SetPlayerObject(player, newPlayer);

            if (StaticServiceLocator.Instance.TryGet<InGameManager>(out var inGameManager))
                inGameManager.AddPlayerObject(player, newPlayer);
        }

        /// <summary>
        /// 新しく生成したキャラクターオブジェクトが正常に動作するための初期化処理メソッド
        /// </summary>
        /// <param name="player"></param>
        /// <param name="newPlayer"></param>
        private static void InitializeReplacementPlayer(PlayerRef player, NetworkObject newPlayer)
        {
            if (PlayerDatabase.Instance.PlayerDataDic.TryGet(player, out var playerData))
            {
                var buildGenerator = newPlayer.GetComponentInChildren<BuildGenerator>();
                if (buildGenerator)
                    buildGenerator.GenerateBuild(playerData.BuildType);
            }

            if (!StaticServiceLocator.Instance.TryGet<InGameManager>(out var inGameManager))
                return;

            var playerHealth = newPlayer.GetComponent<PlayerHealth>();
            if (!playerHealth)
                return;

            var killUseCase = new PlayerKillUseCase(inGameManager.GameRule.PlayerKilledStrategy);
            playerHealth.OnDeath += hitData =>
            {
                inGameManager.PlayerKilled?.Invoke(hitData.ExecutorRef, hitData.TargetRef);
                killUseCase.Execute(hitData);
            };
        }
    }
}
