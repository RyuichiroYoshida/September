using System.Collections.Generic;
using System.Linq;
using Fusion;
using InGame.Common;
using September.Common;
using September.InGame.Common;
using UnityEngine;

namespace InGame.Player.Ability
{
    /// <summary>
    /// 実行しているアビリティの情報を保持するクラス
    /// </summary>
    public class AbilityRuntimeInfo
    {
        public AbilityBase Instance;
        public bool RunLocal;
        public bool IsAuthorityInstance;
    }
    
    /// <summary>
    /// アビリティを実行するクラス
    /// アビリティの更新処理はサーバーで行い、RPCでクライアントとアビリティの実行状態を同期します。
    /// </summary>
    public class AbilityExecutor : NetworkBehaviour
    {
        [SerializeReference, SubclassSelector] private List<AbilityBase> _abilityReferences = new();
        [SerializeField] private bool _isInitialized = false;
        private ISpawner _spawner;
        private readonly Dictionary<PlayerRef, List<AbilityRuntimeInfo>> _playerActiveAbilityInfo = new();
        private readonly List<(PlayerRef player, AbilityRuntimeInfo info)> _pendingRemovals = new();

        public static AbilityExecutor Instance { get; private set; }
        public Dictionary<PlayerRef, List<AbilityRuntimeInfo>> PlayerActiveAbilityInfo => _playerActiveAbilityInfo;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Initialize()
        {
            _spawner = StaticServiceLocator.Instance.Get<ISpawner>();
            _isInitialized = true;
        }

        public void RequestAbilityExecution(AbilityContext context)
        {
            if (!_isInitialized) Initialize();
            
            // まずアビリティ参照を取得してRunLocalを確認
            var abilityRef = _abilityReferences.Find(x => x.AbilityName == context.AbilityName);
            if (abilityRef == null)
            {
                Debug.LogWarning($"Ability '{context.AbilityName}' の参照が見つかりませんでした");
                return;
            }

            // RunLocalの場合は完全にローカルで処理
            if (abilityRef.RunLocal)
            {
                ExecuteLocalAbility(context);
                return;
            }

            // サーバー処理のアビリティ
            if (Runner.IsServer)
            {
                if (context.ActionType == AbilityActionType.発動)
                    TryExecuteAbilityInternal(context, true);
                else if (context.ActionType == AbilityActionType.停止)
                    StopAbilityInternal(context);
            }
            else
            {
                RPC_ExecuteAbilityRequest(context);
            }
        }

        /// <summary>
        /// ローカル専用アビリティの実行
        /// サーバーには送信せず、クライアント上でのみ実行
        /// </summary>
        private void ExecuteLocalAbility(AbilityContext context)
        {
            if (context.ActionType == AbilityActionType.発動)
                TryExecuteLocalAbilityInternal(context);
            else if (context.ActionType == AbilityActionType.停止)
                StopLocalAbilityInternal(context);
        }

        private void TryExecuteLocalAbilityInternal(AbilityContext context)
        {
            var abilityRef = _abilityReferences.Find(x => x.AbilityName == context.AbilityName);
            if (abilityRef is not { RunLocal: true }) return;

            var abilityInstance = abilityRef.Clone(abilityRef);
            var activeAbilityInfo = _playerActiveAbilityInfo
                .Where(x => x.Key == context.SourcePlayer)
                .SelectMany(x => x.Value)
                .ToList();
            var initialized = abilityInstance.TryInitializeWithTrigger(context, activeAbilityInfo, _spawner);

            if (initialized)
            {
                abilityInstance.InjectTimeProvider(new PhotonTimeProvider(Runner));
                var runtime = new AbilityRuntimeInfo
                {
                    Instance = abilityInstance,
                    RunLocal = true,
                    IsAuthorityInstance = false // ローカル実行なのでfalse
                };
                
                if (!_playerActiveAbilityInfo.ContainsKey(context.SourcePlayer))
                {
                    _playerActiveAbilityInfo[context.SourcePlayer] = new List<AbilityRuntimeInfo>();
                }
                _playerActiveAbilityInfo[context.SourcePlayer].Add(runtime);

                // ローカル処理の終了処理
                abilityInstance.OnEndAbilityEvent += () =>
                {
                    _pendingRemovals.Add((context.SourcePlayer, runtime));
                };
            }
        }

        private void StopLocalAbilityInternal(AbilityContext context)
        {
            if (!_playerActiveAbilityInfo.TryGetValue(context.SourcePlayer, out var abilityList)) return;

            if (context.AbilityName == AbilityName.全てのアビリティ)
            {
                // ローカル実行のアビリティのみ停止
                var localAbilities = abilityList.Where(x => x.RunLocal).ToList();
                foreach (var runtime in localAbilities)
                {
                    runtime.Instance.ForceEnd();
                }
            }
            else
            {
                var target = abilityList.FirstOrDefault(x => x.Instance.AbilityName == context.AbilityName && x.RunLocal);
                target?.Instance.ForceEnd();
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_ExecuteAbilityRequest(AbilityContext context)
        {
            // サーバー処理のアビリティのみ受け付け
            var abilityRef = _abilityReferences.Find(x => x.AbilityName == context.AbilityName);
            if (abilityRef == null || abilityRef.RunLocal) return; // RunLocalは除外

            if (context.ActionType == AbilityActionType.発動)
                TryExecuteAbilityInternal(context, true);
            else if (context.ActionType == AbilityActionType.停止)
                StopAbilityInternal(context);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_SyncAbilityState(AbilityContext context)
        {
            if (!HasStateAuthority)
            {
                // サーバー処理のアビリティのみ同期
                var abilityRef = _abilityReferences.Find(x => x.AbilityName == context.AbilityName);
                if (abilityRef is { RunLocal: false })
                {
                    TryExecuteAbilityInternal(context, false);
                }
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_RemoveAbilityInstance(AbilityContext context)
        {
            if (!_playerActiveAbilityInfo.TryGetValue(context.SourcePlayer, out var list)) return;
            // サーバー処理のアビリティのみ削除対象
            var targetRuntime = list.FirstOrDefault(x => x.Instance.AbilityName == context.AbilityName && !x.RunLocal);
            if (targetRuntime != null)
            {
                _pendingRemovals.Add((context.SourcePlayer, targetRuntime));
            }
        }

        private void TryExecuteAbilityInternal(AbilityContext context, bool isAuthority)
        {
            var abilityRef = _abilityReferences.Find(x => x.AbilityName == context.AbilityName);
            if (abilityRef == null)
            {
                Debug.LogWarning("Abilityの参照が見つかりませんでした");
                return;
            }

            // RunLocalのアビリティはサーバーでは実行しない
            if (abilityRef.RunLocal) return;

            var abilityInstance = abilityRef.Clone(abilityRef);
            var activeAbilityInfo = _playerActiveAbilityInfo
                .Where(x => x.Key == context.SourcePlayer)
                .SelectMany(x => x.Value)
                .Where(x => !x.RunLocal) // ローカル実行のものは除外
                .ToList();
            var initialized = abilityInstance.TryInitializeWithTrigger(context, activeAbilityInfo, _spawner);

            if (initialized)
            {
                abilityInstance.InjectTimeProvider(new PhotonTimeProvider(Runner));
                var runtime = new AbilityRuntimeInfo
                {
                    Instance = abilityInstance,
                    RunLocal = abilityInstance.RunLocal,
                    IsAuthorityInstance = isAuthority
                };
                if (!_playerActiveAbilityInfo.ContainsKey(context.SourcePlayer))
                {
                    _playerActiveAbilityInfo[context.SourcePlayer] = new List<AbilityRuntimeInfo>();
                }
                _playerActiveAbilityInfo[context.SourcePlayer].Add(runtime);

                if (Runner.IsServer && isAuthority)
                {
                    abilityInstance.OnEndAbilityEvent += () =>
                    {
                        _pendingRemovals.Add((context.SourcePlayer, runtime));
                        RPC_RemoveAbilityInstance(context);
                    };
                    RPC_SyncAbilityState(context);
                }
            }
        }

        private void StopAbilityInternal(AbilityContext context)
        {
            if (!_playerActiveAbilityInfo.TryGetValue(context.SourcePlayer, out var abilityList)) return;

            if (context.AbilityName == AbilityName.全てのアビリティ)
            {
                // サーバー処理のアビリティのみ停止
                var serverAbilities = abilityList.Where(x => !x.RunLocal).ToList();
                foreach (var runtime in serverAbilities)
                {
                    runtime.Instance.ForceEnd();
                }

                if (HasStateAuthority)
                {
                    foreach (var runtime in serverAbilities)
                    {
                        var removalContext = new AbilityContext
                        {
                            AbilityName = runtime.Instance.AbilityName,
                            SourcePlayer = context.SourcePlayer
                        };
                        RPC_RemoveAbilityInstance(removalContext);
                    }
                }
            }
            else
            {
                var target = abilityList.FirstOrDefault(x => x.Instance.AbilityName == context.AbilityName && !x.RunLocal);
                if (target == null) return;
                target.Instance.ForceEnd();

                if (HasStateAuthority)
                {
                    RPC_RemoveAbilityInstance(context);
                }
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!_isInitialized) Initialize();
            
            foreach (var activeAbility in _playerActiveAbilityInfo)
            {
                foreach (var runtimeAbility in activeAbility.Value)
                {
                    // Tick処理の分岐
                    if (runtimeAbility.RunLocal)
                    {
                        // ローカル実行アビリティは常に実行
                        runtimeAbility.Instance.Tick(Runner.DeltaTime);
                    }
                    else if (runtimeAbility.IsAuthorityInstance)
                    {
                        // サーバー処理アビリティは権威インスタンスのみ実行
                        runtimeAbility.Instance.Tick(Runner.DeltaTime);
                    }
                }
            }

            // 削除処理
            foreach (var (player, info) in _pendingRemovals)
            {
                if (_playerActiveAbilityInfo.TryGetValue(player, out var list))
                {
                    list.RemoveAll(x => x.Instance == info.Instance);
                }
            }
            _pendingRemovals.Clear();
        }

        public float? GetCooldown(PlayerRef player, AbilityName ability)
        {
            if (!_playerActiveAbilityInfo.TryGetValue(player, out var list)) return null;
            var runtime = list.Find(x => x.Instance.AbilityName == ability);
            return runtime?.Instance.CurrentCooldown;
        }
    }
}