using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using September.Common;
using September.InGame.Rules;
using September.NewResult;

namespace September.InGame.Common
{
    /// <summary>
    /// インゲーム中のリアルタイムなランキング情報を計算し保持するクラス
    /// </summary>
    public class Ranking : IDisposable
    {
        private readonly Dictionary<PlayerRef, int> _playerRanks = new();
        private PlayerResultEntry[] _rankedPlayers = Array.Empty<PlayerResultEntry>();

        /// <summary>
        /// プレイヤーごとの順位の読み取り専用マップ
        /// </summary>
        public IReadOnlyDictionary<PlayerRef, int> PlayerRanks => _playerRanks;

        /// <summary>
        /// 順位順にソートされたプレイヤー情報のリザルトエントリリスト
        /// </summary>
        public IReadOnlyList<PlayerResultEntry> RankedPlayers => _rankedPlayers;

        /// <summary>
        /// ランキング情報が更新されたときに発火するイベント
        /// </summary>
        public event Action OnRankingUpdated;

        /// <summary>
        /// 初期化処理を行い、PlayerDatabaseの更新イベントを購読します。
        /// </summary>
        public void Initialize()
        {
            if (PlayerDatabase.Instance != null)
            {
                PlayerDatabase.Instance.ChangedDataAction += OnPlayerDataChanged;
                CalculateRanking(PlayerDatabase.Instance.PlayerDataDic);
            }
        }

        private void OnPlayerDataChanged(NetworkDictionary<PlayerRef, SessionPlayerData> playerDataDic)
        {
            CalculateRanking(playerDataDic);
        }

        /// <summary>
        /// リアルタイムのランキングを計算します。
        /// </summary>
        public void CalculateRanking(NetworkDictionary<PlayerRef, SessionPlayerData> playerDataDic)
        {
            if (IGameRule.CurrentRule == null) return;

            var rankingPolicy = IGameRule.CurrentRule.RankingPolicy;
            var scorePolicy = IGameRule.CurrentRule.GameResultScorePolicy;

            if (rankingPolicy == null || scorePolicy == null) return;

            var playerResults = new List<KeyValuePair<PlayerRef, PlayerResultEntry>>();
            var runner = PlayerDatabase.Instance != null ? PlayerDatabase.Instance.Runner : null;

            foreach ((PlayerRef player, SessionPlayerData sessionPlayerData) in playerDataDic)
            {
                int score = 0;
                try
                {
                    score = scorePolicy.GetScore(player);
                }
                catch (Exception)
                {
                    // オブジェクトが未生成などの理由でGetScoreが失敗した場合は、SessionDataの同期スコアをフォールバックとして使用
                    score = sessionPlayerData.Score;
                }

                var entry = new PlayerResultEntry(
                    sessionPlayerData.DisplayNickName,
                    sessionPlayerData.CharacterType,
                    Array.Empty<ResultExhibitScoreEntry>(),
                    score,
                    sessionPlayerData.IsOgre,
                    runner != null && player == runner.LocalPlayer,
                    sessionPlayerData.DamageDealt,
                    sessionPlayerData.DamageReceived,
                    sessionPlayerData.TotalInteractCount,
                    sessionPlayerData.OgreCount
                );
                playerResults.Add(new KeyValuePair<PlayerRef, PlayerResultEntry>(player, entry));
            }

            // 順位付けポリシーを適用
            var entriesToRank = playerResults.Select(kv => kv.Value).ToArray();
            var rankedEntries = rankingPolicy.Apply(entriesToRank).ToArray();
            _rankedPlayers = rankedEntries;

            // PlayerRef -> Rank のマッピングを作成
            _playerRanks.Clear();
            foreach (var kvp in playerResults)
            {
                var player = kvp.Key;
                var originalEntry = kvp.Value;
                // DisplayNickNameが一致するものを探して順位を取得
                var rankedEntry = rankedEntries.FirstOrDefault(r => r.PlayerName == originalEntry.PlayerName);
                _playerRanks[player] = rankedEntry.Rank;
            }

            OnRankingUpdated?.Invoke();
        }

        /// <summary>
        /// 指定したプレイヤーの現在の順位を取得します。見つからない場合は0を返します。
        /// </summary>
        public int GetRank(PlayerRef player)
        {
            return _playerRanks.GetValueOrDefault(player, 0);
        }

        /// <summary>
        /// 順位情報の総数を取得します。プレイヤー人数と一致します。
        /// </summary>
        /// <returns></returns>
        public int GetCount()
        {
            return _playerRanks.Count;
        }

        public void Dispose()
        {
            if (PlayerDatabase.Instance != null)
            {
                PlayerDatabase.Instance.ChangedDataAction -= OnPlayerDataChanged;
            }
        }
    }
}
