using System.Collections.Generic;
using System.Linq;
using Fusion;
using InGame.Common;
using September.Common;
using UnityEngine;

namespace InGame.Player.Ability
{
    public class AbilityRuntimeInfo
    {
        public AbilityBase Instance;
        public bool RunLocal;
        public bool IsAuthorityInstance;
    }

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

            var abilityRef = _abilityReferences.Find(x => x.AbilityName == context.AbilityName);
            if (abilityRef == null)
            {
                Debug.LogWarning($"Ability '{context.AbilityName}' の参照が見つかりませんでした");
                return;
            }

            if (abilityRef.RunLocal)
            {
                if (context.ActionType == AbilityActionType.発動)
                    TryExecuteAbilityUnified(context, isAuthority: false);
                else
                    StopAbilityUnified(context, isLocal: true);
            }
            else
            {
                if (Runner.IsServer)
                {
                    if (context.ActionType == AbilityActionType.発動)
                        TryExecuteAbilityUnified(context, isAuthority: true);
                    else
                        StopAbilityUnified(context, isLocal: false);
                }
                else
                {
                    RPC_ExecuteAbilityRequest(context);
                }
            }
        }

        private void TryExecuteAbilityUnified(AbilityContext context, bool isAuthority)
        {
            var abilityRef = _abilityReferences.Find(x => x.AbilityName == context.AbilityName);
            if (abilityRef == null) return;

            var abilityInstance = abilityRef.Clone(abilityRef);
            var activeAbilityInfo = _playerActiveAbilityInfo
                .Where(x => x.Key == context.SourcePlayer)
                .SelectMany(x => x.Value)
                .Where(x => x.RunLocal == abilityRef.RunLocal)
                .ToList();

            var initialized = abilityInstance.TryInitializeWithTrigger(context, activeAbilityInfo, _spawner);
            if (!initialized) return;

            abilityInstance.InjectTimeProvider(new PhotonTimeProvider(Runner));

            var runtime = new AbilityRuntimeInfo
            {
                Instance = abilityInstance,
                RunLocal = abilityInstance.RunLocal,
                IsAuthorityInstance = isAuthority
            };

            if (!_playerActiveAbilityInfo.ContainsKey(context.SourcePlayer))
            {
                _playerActiveAbilityInfo[context.SourcePlayer] = new();
            }

            _playerActiveAbilityInfo[context.SourcePlayer].Add(runtime);

            abilityInstance.OnEndAbilityEvent += () =>
            {
                _pendingRemovals.Add((context.SourcePlayer, runtime));
                if (!runtime.RunLocal && Runner.IsServer && runtime.IsAuthorityInstance)
                {
                    RPC_RemoveAbilityInstance(context);
                }
            };

            if (!runtime.RunLocal && Runner.IsServer && isAuthority)
            {
                RPC_SyncAbilityState(context);
            }
        }

        private void StopAbilityUnified(AbilityContext context, bool isLocal)
        {
            if (!_playerActiveAbilityInfo.TryGetValue(context.SourcePlayer, out var abilityList)) return;

            var targetAbilities = abilityList
                .Where(x => x.RunLocal == isLocal &&
                            (context.AbilityName == AbilityName.全てのアビリティ || x.Instance.AbilityName == context.AbilityName))
                .ToList();

            foreach (var runtime in targetAbilities)
            {
                runtime.Instance.ForceEnd();

                if (!isLocal && HasStateAuthority)
                {
                    RPC_RemoveAbilityInstance(new AbilityContext
                    {
                        AbilityName = runtime.Instance.AbilityName,
                        SourcePlayer = context.SourcePlayer
                    });
                }
            }
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_ExecuteAbilityRequest(AbilityContext context)
        {
            var abilityRef = _abilityReferences.Find(x => x.AbilityName == context.AbilityName);
            if (abilityRef == null || abilityRef.RunLocal) return;

            if (context.ActionType == AbilityActionType.発動)
                TryExecuteAbilityUnified(context, isAuthority: true);
            else
                StopAbilityUnified(context, isLocal: false);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_SyncAbilityState(AbilityContext context)
        {
            if (!HasStateAuthority)
            {
                var abilityRef = _abilityReferences.Find(x => x.AbilityName == context.AbilityName);
                if (abilityRef is { RunLocal: false })
                {
                    TryExecuteAbilityUnified(context, isAuthority: false);
                }
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_RemoveAbilityInstance(AbilityContext context)
        {
            if (!_playerActiveAbilityInfo.TryGetValue(context.SourcePlayer, out var list)) return;
            var targetRuntime = list.FirstOrDefault(x => x.Instance.AbilityName == context.AbilityName && !x.RunLocal);
            if (targetRuntime != null)
            {
                _pendingRemovals.Add((context.SourcePlayer, targetRuntime));
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!_isInitialized) Initialize();

            foreach (var activeAbility in _playerActiveAbilityInfo)
            {
                foreach (var runtimeAbility in activeAbility.Value)
                {
                    if (runtimeAbility.RunLocal || runtimeAbility.IsAuthorityInstance)
                    {
                        runtimeAbility.Instance.Tick(Runner.DeltaTime);
                    }
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
