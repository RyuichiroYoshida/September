using System.Collections.Generic;
using System.Linq;
using Fusion;
using September.Common;
using September.InGame.Common;
using September.InGame.Rules.ScorePolicy;
using UnityEngine;

namespace September.InGame.Kraken
{
    public class KrakenEvent : NetworkBehaviour
    {
        [SerializeField] private Kraken _krakenPrefab;
        [SerializeField] private Transform[] _spawnPoints;

        [Header("クラーケンが出現するタイミング（ゲーム開始からの経過時間）")]
        [SerializeField] private float[] _krakenAppearanceTimes;

        private int _krakenAppearanceIndex;

        private IGameResultScorePolicy _scorePolicy;

        private bool _isGameStarted;
        private int _gameStartTick;

        public override void Spawned()
        {
            var inGameManager = StaticServiceLocator.Instance.Get<InGameManager>();
            _scorePolicy = inGameManager.GameRule.GameResultScorePolicy;
            inGameManager.GameStarted += () =>
            {
                _isGameStarted = true;
                _gameStartTick = Runner.Tick;
            };
        }

        public override void FixedUpdateNetwork()
        {
            if (!Runner.IsServer) return;

            if (!_isGameStarted) return;

            if (_krakenAppearanceIndex >= _krakenAppearanceTimes.Length) return;

            if ((Runner.Tick - _gameStartTick) > ToTick(_krakenAppearanceTimes[_krakenAppearanceIndex]))
            {
                IEnumerable<(PlayerRef player, NetworkObject playerObj, int score)> scoreTable
                    = PlayerDatabase.Instance.PlayerObjectDic.Select(kvp => (kvp.Key, kvp.Value, _scorePolicy.GetScore(kvp.Key)));

                var target = scoreTable.OrderBy(e => e.score).Last().playerObj; // 非効率
                var spawnPoint = GetNearestSpawnPoint(target.transform.position);

                Runner.Spawn(_krakenPrefab, spawnPoint.position, spawnPoint.rotation);
                _krakenAppearanceIndex++;
            }
        }

        private int ToTick(float time)
        {
            return (int)(time * Runner.TickRate);
        }

        private Transform GetNearestSpawnPoint(Vector3 position)
        {
            return _spawnPoints
                .Select(spawnPoint => (spawnPoint, distance: (spawnPoint.transform.position - position).sqrMagnitude))
                .OrderBy(t => t.distance) // 非効率
                .First().spawnPoint;
        }
    }
}
