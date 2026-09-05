using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Fusion;
using InGame.Jewelry.Common;
using JetBrains.Annotations;
using September.Common;
using September.InGame.Jewelry;
using September.InGame.UI;
using UnityEngine;
using Random = UnityEngine.Random;

namespace InGame.Jewelry
{
    public class JewelrySpawner : NetworkBehaviour
    {
        [SerializeField] private Transform[] _spawnPositions;
        [SerializeField] private GameTimerData _timerData;
        [SerializeField] private Transform _spawnPredictionRange;
        [SerializeField] private float _predictionVisibleDuration;
        [SerializeField] private JewelrySpawnData _jewelrySpawnData;

        [Header("リポップ設定")]
        [SerializeField] private Vector3 _repopOffset = new(0, 2f, 0);
        [SerializeField] private Vector2 _repopThrowForce = new(3f, 1f);
        [SerializeField] private float _repopDelay = 0f;

        private int _nextTime = 0;

        private CancellationTokenSource _cts;

        public override void Spawned()
        {
            if (!HasStateAuthority)
                return;

            Array.Sort(_jewelrySpawnData.SpawnSettings, (a, b) => b.SpawnTime.CompareTo(a.SpawnTime));

            WaitSpawnAsync().Forget();
        }

        public override void FixedUpdateNetwork()
        {
            foreach ((JewelryType jewelryType, int count) in DespawnedJewelryRepository.GetDespawnedJewelryCount())
            {
                Vector3 spawnPosition = _spawnPositions[Random.Range(0, _spawnPositions.Length)].position + _repopOffset;

                for (int i = 0; i < count; i++)
                {
                    RespawnJewelry(spawnPosition, jewelryType, _repopDelay, _cts.Token).Forget();
                }
            }

            DespawnedJewelryRepository.Clear();

            return;

            async UniTaskVoid RespawnJewelry(Vector3 spawnPosition, JewelryType jewelryType, float delay = 0f, CancellationToken token = default)
            {
                if (delay > 0)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: token);
                }

                NetworkObject obj = SpawnJewelry(spawnPosition, jewelryType);

                if (obj == null) return;

                if (obj.gameObject.TryGetComponent(out Jewelry jewelry))
                {
                    jewelry.JewelryControl.RandomThrow(_repopThrowForce.x, _repopThrowForce.y);
                }
            }
        }

        private async UniTask WaitSpawnAsync()
        {
            _cts = new CancellationTokenSource();
            _nextTime = 0;

            await WaitForSpawnTimingAsync();
        }

        private async UniTask WaitForSpawnTimingAsync()
        {
            int tickRate = Runner.TickRate;
            int gameEndTick = Runner.Tick + Mathf.RoundToInt((_timerData.GameTime + _timerData.PreStartTime) * tickRate);

            int lastTick = Runner.Tick;

            while (Runner.Tick < gameEndTick)
            {
                if (_cts.IsCancellationRequested)
                    return;

                if (Runner.Tick == lastTick)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, _cts.Token);
                    continue;
                }

                lastTick = Runner.Tick;

                int remaining = gameEndTick - Runner.Tick;
                int seconds = Mathf.CeilToInt(remaining / (float)tickRate);

                if (_nextTime >= _jewelrySpawnData.SpawnSettings.Length)
                    return;

                var next = _jewelrySpawnData.SpawnSettings[_nextTime];
                if (seconds <= next.SpawnTime)
                {
                    // ゲーム開始前に生成される場合
                    if (next.SpawnTime > _timerData.GameTime)
                    {
                        SpawnJewelryGroup(next);
                    }
                    else
                    {
                        StartSpawnSequenceAsync(next).Forget();
                    }
                    _nextTime++;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, _cts.Token);
            }
        }

        private async UniTask StartSpawnSequenceAsync(JewelrySpawnSetting spawnSetting)
        {
            if (_spawnPositions == null || _spawnPositions.Length == 0)
            {
                Debug.LogError("JewelrySpawner : スポーン位置が設定されていません。");
                return;
            }

            if (!TryGetSpawnTransform(spawnSetting, out Transform spawnTransform)) return;

            RPC_SetSpawnPrediction(true, spawnTransform.position);

            //スポーン予告メッセージを出す
            if (spawnSetting.ShowSpawnMessage)
                RPC_ShowSpawnMessage(_predictionVisibleDuration);

            await UniTask.WaitForSeconds(_predictionVisibleDuration, cancellationToken: _cts.Token);
            SpawnJewelryGroup(spawnTransform.position, spawnSetting);
            RPC_SetSpawnPrediction(false);
        }

        private bool TryGetSpawnTransform(JewelrySpawnSetting spawnSetting, out Transform spawnTransform)
        {
            if (spawnSetting.PositionIndex < 0)
            {
                int randomIndex = Random.Range(0, _spawnPositions.Length);
                Transform randomTransform = _spawnPositions[randomIndex];
                spawnTransform = randomTransform;
            }
            else
            {
                if (spawnSetting.PositionIndex >= 0 && spawnSetting.PositionIndex < _spawnPositions.Length)
                {
                    spawnTransform = _spawnPositions[spawnSetting.PositionIndex];
                }
                else
                {
                    Debug.LogError("JewelrySpawner : 存在しないインデックス番号です");
                    spawnTransform = null;
                    return false;
                }
            }

            return true;
        }


        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_SetSpawnPrediction(bool visible, Vector3 position = default)
        {
            _spawnPredictionRange.gameObject.SetActive(visible);

            if (visible)
            {
                _spawnPredictionRange.position = position;
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_ShowSpawnMessage(float second)
        {
            UIController.I.ShowStatusUpUI(second, Exhibit.StatusUpType.JewelrySpawn);
        }

        private void SpawnJewelryGroup(JewelrySpawnSetting spawnSetting)
        {
            if (TryGetSpawnTransform(spawnSetting, out Transform spawnTransform))
            {
                SpawnJewelryGroup(spawnTransform.position, spawnSetting);
            }
        }

        private void SpawnJewelryGroup(Vector3 centerPosition, JewelrySpawnSetting spawnSetting)
        {
            centerPosition.y += spawnSetting.Height;

            for (int i = 0; i < spawnSetting.Items.Length; i++)
            {
                int count = spawnSetting.Items[i].Count;
                for (int j = 0; j < count; j++)
                {
                    Vector2 randomOffset = Random.insideUnitCircle * spawnSetting.SpawnRange;

                    Vector3 spawnPosition = centerPosition;
                    spawnPosition.x += randomOffset.x;
                    spawnPosition.z += randomOffset.y;

                    SpawnJewelry(spawnPosition, spawnSetting.Items[i].JewelryType);
                }
            }
        }

        [CanBeNull]
        private NetworkObject SpawnJewelry(Vector3 position, JewelryType jewelryType)
        {
            NetworkObject prefab = _jewelrySpawnData.GetPrefab(jewelryType);

            if (prefab == null)
            {
                Debug.LogError($"JewelrySpawner : 宝石のPrefabが設定されていません。宝石の種類: {jewelryType}");
                return null;
            }

            return Runner.Spawn(prefab, position, Quaternion.identity);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        private void OnDrawGizmosSelected()
        {
            foreach (JewelrySpawnSetting setting in _jewelrySpawnData.SpawnSettings)
            {
                if (setting.PositionIndex >= _spawnPositions.Length) continue;

                if (setting.PositionIndex < 0)
                {
                    Gizmos.color = Color.yellow;
                    foreach (Transform spawnPoint in _spawnPositions)
                    {
                        DrawSpawnArea(spawnPoint.position, setting);
                    }
                }
                else
                {
                    Gizmos.color = Color.cyan;
                    Transform spawnPoint = _spawnPositions[setting.PositionIndex];
                    DrawSpawnArea(spawnPoint.position, setting);
                }
            }

            return;

            void DrawSpawnArea(Vector3 spawnPosition, JewelrySpawnSetting setting)
            {
                Vector3 center = spawnPosition + Vector3.up * setting.Height;
                GizmosUtility.DrawCircle(center, Vector3.up, setting.SpawnRange);
            }
        }
    }
}
