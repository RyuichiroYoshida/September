using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Fusion;
using Result;
using UniRx;
using UnityEngine;

namespace September.Common
{
    public class PlayerDatabase : NetworkBehaviour
    {
        [Header("Not Networked")]
        [SerializeField] private ExhibitScoreConfig _config;

        [Header("Ability Bonus Config")]
        [SerializeField] private ExhibitScoreConfig _okabeRideConfig;
        [SerializeField] private ExhibitScoreConfig _haruDestroyConfig;
        [SerializeField] private int _sarutobiBonusScore = 50;
        [SerializeField] private ExhibitScoreConfig _tanihiraBonusScore;

        [Networked, Capacity(20)]
        public NetworkDictionary<PlayerRef, NetworkObject> PlayerObjectDic => default;
        [Networked, OnChangedRender(nameof(OnChangedPlayerData)), Capacity(8)]
        public NetworkDictionary<PlayerRef, SessionPlayerData> PlayerDataDic => default;
        public Action<NetworkDictionary<PlayerRef, SessionPlayerData>> ChangedDataAction;
        public static PlayerDatabase Instance;

        private Subject<PlayerRef> _onBotJoin = new();
        public IObservable<PlayerRef> OnBotJoin => _onBotJoin;
        private Subject<PlayerRef> _onBotLeft = new();
        public IObservable<PlayerRef> OnBotLeft => _onBotLeft;

        private readonly Dictionary<PlayerRef, ScoreTracker> _serverTrackers = new();
        public static readonly int BotStartIndex = 100;

        public override void Spawned()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                AbilityBonusContainer.Init(_okabeRideConfig, _haruDestroyConfig, _sarutobiBonusScore, _tanihiraBonusScore);
            }
            else
            {
                Runner.Despawn(Object);
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (Instance == this)
                Instance = null;

            _serverTrackers.Clear();
        }

        public void Server_AddExhibit(PlayerRef actor, ExhibitType type)
        {
            if (!Object.HasStateAuthority)
                return;

            if (!_serverTrackers.TryGetValue(actor, out ScoreTracker tracker))
                _serverTrackers[actor] = tracker = new ScoreTracker(_config);

            tracker.AddInteract(type);
            UpdatePlayerScore(actor, tracker);
        }

        public void Server_AddGrapplingHook(PlayerRef actor)
        {
            if (!Object.HasStateAuthority)
                return;

            if (!_serverTrackers.TryGetValue(actor, out ScoreTracker tracker))
                _serverTrackers[actor] = tracker = new ScoreTracker(_config);

            tracker.AddGrapplingHook();
            UpdatePlayerScore(actor, tracker);
        }

        // スコアを再計算
        public void Server_RecalculateScore(PlayerRef actor)
        {
            if (!Object.HasStateAuthority)
                return;

            if (!_serverTrackers.TryGetValue(actor, out var tracker))
                _serverTrackers[actor] = tracker = new ScoreTracker(_config);

            UpdatePlayerScore(actor, tracker);
        }

        // ハルクのDestroy処理をここで加算
        public void Server_AddDestroyExhibit(PlayerRef actor, ExhibitType type)
        {
            if (!Object.HasStateAuthority)
                return;

            if (!_serverTrackers.TryGetValue(actor, out ScoreTracker tracker))
            {
                _serverTrackers[actor] = tracker = new ScoreTracker(_config);
            }

            tracker.AddDestroyed(type);
            UpdatePlayerScore(actor, tracker);
        }

        public void Server_AddKrakenDamageScore(PlayerRef actor, int damage)
        {
            if (!Object.HasStateAuthority)
                return;

            if (!_serverTrackers.TryGetValue(actor, out ScoreTracker tracker))
                _serverTrackers[actor] = tracker = new ScoreTracker(_config);

            tracker.AddKrakenDamage(damage);
            UpdatePlayerScore(actor, tracker);
        }

        // 合計スコア取得
        public int Server_GetTotal(PlayerRef actor)
        {
            if (!_serverTrackers.TryGetValue(actor, out ScoreTracker tracker))
                return 0;

            if (!PlayerDataDic.TryGet(actor, out SessionPlayerData d))
                return tracker.CalcTotal(default);

            return tracker.CalcTotal(d) + AbilityBonusContainer.CalcBonus(d.CharacterType, tracker);
        }

        public void Server_AddDamageDealt(PlayerRef actor, int damage)
        {
            if (!TryGetPlayerSessionData(actor, out var data)) return;
            data.DamageDealt += damage;
            PlayerDataDic.Set(actor, data);
        }

        public void Server_AddDamageReceived(PlayerRef actor, int damage)
        {
            if (!TryGetPlayerSessionData(actor, out var data)) return;
            data.DamageReceived += damage;
            PlayerDataDic.Set(actor, data);
        }

        public void Server_AddOgreCount(PlayerRef actor)
        {
            if (!TryGetPlayerSessionData(actor, out var data)) return;
            data.OgreCount += 1;
            PlayerDataDic.Set(actor, data);
        }

        private bool TryGetPlayerSessionData(PlayerRef actor, out SessionPlayerData data)
        {
            return PlayerDataDic.TryGet(actor, out data) && Object.HasStateAuthority;
        }

        private void UpdatePlayerScore(PlayerRef actor, ScoreTracker tracker)
        {
            if (!PlayerDataDic.TryGet(actor, out SessionPlayerData d))
                return;

            int baseScore = tracker.CalcTotal(d);
            int bonus = AbilityBonusContainer.CalcBonus(d.CharacterType, tracker);

            d.Score = baseScore + bonus;
            d.TotalInteractCount = tracker.GetTotalInteractCount();
            PlayerDataDic.Set(actor, d);
        }

        // ホストからクライアントに送信
        public void Server_PushResultToClients()
        {
            if (!Object.HasStateAuthority)
                return;

            foreach (var kv in PlayerDataDic)
            {
                PlayerRef player = kv.Key;
                SessionPlayerData data = kv.Value;
                _serverTrackers.TryGetValue(player, out ScoreTracker tracker);

                if (tracker == null)
                {
                    Debug.LogWarning($"[ResultPush] No tracker for {player}.");
                    Rpc_SendPersonalResult(player, "", data.Score);
                    continue;
                }

                int calc = tracker.CalcTotal(data) + AbilityBonusContainer.CalcBonus(data.CharacterType, tracker);

                if (calc != data.Score)
                {
                    // エラーだしてもいいかも
                    Debug.LogWarning($"[ResultPush] Mismatch {player}: serverCalc={calc}, netScore={data.Score}. Overwrite netScore.");
                    data.Score = calc;
                    PlayerDataDic.Set(player, data);
                }

                string payload = EncodeDetailV2(tracker);
                Rpc_SendPersonalResult(player, payload, calc);
            }
        }

        private static string EncodeDetailV2(ScoreTracker tracker)
        {
            StringBuilder sb = new();
            sb.Append("V2|E:");
            bool first = true;
            foreach (KeyValuePair<ExhibitType, int> kv in tracker.ExhibitCounts)
            {
                if (!first)
                    sb.Append(',');

                sb.Append(kv.Key).Append('=').Append(kv.Value);
                first = false;
            }

            if (tracker.DestroyedExhibitCounts.Count > 0)
            {
                sb.Append("|D:");
                first = true;
                foreach (KeyValuePair<ExhibitType, int> kv in tracker.DestroyedExhibitCounts)
                {
                    if (!first)
                    {
                        sb.Append(',');
                    }
                    sb.Append(kv.Key).Append('=').Append(kv.Value);
                    first = false;
                }
            }
            sb.Append("|G:").Append(tracker.GrapplingHookCount);
            sb.Append("|F:").Append(tracker.FriendExhibitCount);
            sb.Append("|K:").Append(tracker.KrakenDamageScore);
            return sb.ToString();
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void Rpc_SendPersonalResult(PlayerRef target, string encodedPayload, int pageTotal)
        {
            if (Runner.LocalPlayer != target)
                return;

            if (!ResultDataInbox.I)
            {
                GameObject go = new("[ResultDataInbox]");
                go.AddComponent<ResultDataInbox>();
            }
            ResultDataInbox.I.LoadFromEncoded(encodedPayload, pageTotal);
        }

        public void AddPlayerData(PlayerRef playerRef)
        {
            if (playerRef != Runner.LocalPlayer && playerRef.AsIndex < BotStartIndex) return;
            var localNickName = NickNameProvider.GetNickName();
            var nickNameOrder = PlayerDataDic.Count(kv => kv.Value.PureNickName == localNickName);
            Rpc_SetPlayerData(playerRef, new SessionPlayerData(localNickName, nickNameOrder));
        }

        public void AddBotData()
        {
            int botIndex = GetBotIndex();

            var localNickName = "Bot";
            var nickNameOrder = botIndex - BotStartIndex;
            Rpc_SetBotData(PlayerRef.FromIndex(botIndex), new SessionPlayerData(localNickName, nickNameOrder));
        }

        public void RemoveBotData(PlayerRef playerRef)
        {
            if (!HasStateAuthority) return;
            Rpc_RemoveBotData(playerRef);
        }

        public void AddPlayerObject(PlayerRef playerRef, NetworkObject playerObject)
        {
            PlayerObjectDic.Set(playerRef, playerObject);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
        private void Rpc_SetPlayerData(PlayerRef playerRef, SessionPlayerData data)
        {
            PlayerDataDic.Set(playerRef, data);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All, Channel = RpcChannel.Reliable)]
        private void Rpc_SetBotData(PlayerRef playerRef, SessionPlayerData data)
        {
            PlayerDataDic.Set(playerRef, data);
            _onBotJoin.OnNext(playerRef);
            Debug.Log(playerRef.AsIndex + "Bot");
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
        public void Rpc_SetCharacter(PlayerRef playerRef, CharacterType characterType)
        {
            if (!PlayerDataDic.TryGet(playerRef, out var playerData)) return;
            playerData.CharacterType = characterType;
            PlayerDataDic.Set(playerRef, playerData);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All, Channel = RpcChannel.Reliable)]
        public void Rpc_RemoveBotData(PlayerRef playerRef)
        {
            if (HasStateAuthority)
            {
                if (!PlayerDataDic.TryGet(playerRef, out _)) return;
                PlayerDataDic.Remove(playerRef);
            }
            _onBotLeft.OnNext(playerRef);
        }

        /// <summary>
        /// BotのIndex100以上で使われていないIndexを返す
        /// </summary>
        /// <returns></returns>
        private int GetBotIndex()
        {
            List<int> nodeIndex = new();
            foreach (var kv in PlayerDataDic)
            {
                if (kv.Key.AsIndex >= BotStartIndex)
                {
                    nodeIndex.Add(kv.Key.AsIndex - BotStartIndex);
                }
            }

            nodeIndex.Sort();
            int count = 0;
            while (count < nodeIndex.Count && nodeIndex[count] == count)
            {
                count++;
            }

            int result = count + BotStartIndex;
            if (result < BotStartIndex)
            {
                Debug.LogError("Botに使えないIndexです");
                return 999;
            }

            return result;
        }

        /// <summary>
        /// 決定したビルドルートを保存するメソッド
        /// </summary>
        /// <param name="playerRef">プレイヤーの情報</param>
        /// <param name="buildType">決定したビルドルート</param>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
        public void Rpc_SetBuild(PlayerRef playerRef, BuildRouteType buildType)
        {
            if (!PlayerDataDic.TryGet(playerRef, out var playerData)) return;
            playerData.BuildType = buildType;
            PlayerDataDic.Set(playerRef, playerData);
        }

        private void OnChangedPlayerData()
        {
            ChangedDataAction?.Invoke(PlayerDataDic);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            _serverTrackers.Clear();
        }
    }
}
