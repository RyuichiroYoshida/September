using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Fusion;
using NaughtyAttributes;
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
            
            if (!_isInitialized) Initialize();
        }

        private void Initialize()
        {
            _spawner = GetComponent<ISpawner>();
            _isInitialized = true;
        }

        public void RequestAbilityExecution(AbilityContext context)
        {
            if (!_isInitialized) Initialize();
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

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_ExecuteAbilityRequest(AbilityContext context)
        {
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
                TryExecuteAbilityInternal(context, false);
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_RemoveAbilityInstance(AbilityContext context)
        {
            //if (!HasStateAuthority) return;
            if (!_playerActiveAbilityInfo.TryGetValue(context.SourcePlayer, out var list)) return;
            _pendingRemovals.Add((context.SourcePlayer, list.FirstOrDefault(x => x.Instance.AbilityName == context.AbilityName)));
        }

        private void TryExecuteAbilityInternal(AbilityContext context, bool isAuthority)
        {
            var abilityRef = _abilityReferences.Find(x => x.AbilityName == context.AbilityName);
            if (abilityRef == null)
            {
                Debug.LogWarning("Abilityの参照が見つかりませんでした");
                return;
            }

            var abilityInstance = abilityRef.Clone(abilityRef);
            var activeAbilityInfo = _playerActiveAbilityInfo
                .Where(x => x.Key == context.SourcePlayer)
                .SelectMany(x => x.Value)
                .ToList();
            var initialized = abilityInstance.TryInitializeWithTrigger(context, activeAbilityInfo, _spawner);

            if (initialized)
            {
                abilityInstance.ResetSharedVariable();
                var runtime = new AbilityRuntimeInfo
                {
                    Instance = abilityInstance,
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
                foreach (var runtime in abilityList)
                {
                    runtime.Instance.ForceEnd();
                }

                if (HasStateAuthority)
                {
                    foreach (var runtime in abilityList)
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
                var target = abilityList.FirstOrDefault(x => x.Instance.AbilityName == context.AbilityName);
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
                    runtimeAbility.Instance.CalculateSharedVariable(Runner.DeltaTime);
                    if (!runtimeAbility.IsAuthorityInstance) continue;
                    runtimeAbility.Instance.Tick(Runner.DeltaTime);
                }
            }

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
