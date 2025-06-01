using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using InGame.Common;
using September.Common;
using UnityEngine;
using September.InGame.Common;

namespace InGame.Player.Ability
{
    public class AbilityExecutor : NetworkBehaviour, IAbilityExecutor, IRegisterableService
    {
        [SerializeReference, SubclassSelector] private List<AbilityBase> _abilityReferences = new();
        private readonly Dictionary<int, List<AbilityRuntimeInfo>> _playerActiveAbilityInfo = new();
        private bool _isInitialized = false;
        private bool _abilityStateDirty = false;
        private ISpawner _spawner;

        private void Awake()
        {
            Register(StaticServiceLocator.Instance);
        }

        private void Initialize()
        {
            _spawner = StaticServiceLocator.Instance.Get<ISpawner>();
            _isInitialized = true;
        }

        public Dictionary<int, List<AbilityRuntimeInfo>> PlayerActiveAbilityInfo => _playerActiveAbilityInfo;

        public void RequestAbilityExecution(AbilityContext context)
        {
            if (!_isInitialized) Initialize();

            if (Runner.IsServer)
            {
                TryExecuteAbilityUnified(context, isAuthority: true);
                _abilityStateDirty = true;
            }
            else
            {
                RPC_RequestAbility(context);
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
                .ToList();

            if (!abilityInstance.TryInitializeWithTrigger(context, activeAbilityInfo, _spawner)) return;

            var runtime = new AbilityRuntimeInfo
            {
                Instance = abilityInstance,
                RunLocal = abilityInstance.RunLocal,
                IsAuthorityInstance = isAuthority
            };

            if (!_playerActiveAbilityInfo.ContainsKey(context.SourcePlayer))
                _playerActiveAbilityInfo[context.SourcePlayer] = new();

            _playerActiveAbilityInfo[context.SourcePlayer].Add(runtime);
            Debug.Log($"Ability {abilityInstance.AbilityName} is running: {abilityInstance.AfterCooldown}");

            _abilityStateDirty = true;
        }

        private void Update()
        {
            if (!_isInitialized) Initialize();
            if (!Runner || Runner.IsServer) return;

            foreach (var activeAbility in _playerActiveAbilityInfo)
            {
                foreach (var runtime in activeAbility.Value)
                {
                    runtime.Instance.CalculateSharedVariable(Time.deltaTime);
                }
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!_isInitialized) Initialize();

            foreach (var activeAbility in _playerActiveAbilityInfo)
            {
                foreach (var runtime in activeAbility.Value)
                {
                    Debug.Log($"Ability {runtime.Instance.AbilityName} is running: {runtime.Instance.AfterCooldown}");
                    if (runtime.RunLocal || runtime.IsAuthorityInstance)
                    {
                        runtime.Instance.Tick(Runner.DeltaTime);
                    }

                    runtime.Instance.CalculateSharedVariable(Runner.DeltaTime);
                }
            }

            foreach (var kvp in _playerActiveAbilityInfo)
            {
                int beforeCount = kvp.Value.Count;
                kvp.Value.RemoveAll(runtime => runtime.Instance.AfterCooldown && runtime.Instance.Phase == AbilityBase.AbilityPhase.Ended);
                if (kvp.Value.Count != beforeCount)
                {
                    _abilityStateDirty = true;
                }
            }

            if (Runner.IsServer && _abilityStateDirty)
            {
                SendAbilityStateSnapshot();
            }
            _abilityStateDirty = false;
        }

        private void SendAbilityStateSnapshot()
        {
            var playerIds = new List<int>();
            var abilityNames = new List<int>();
            var isRunningArray = new List<bool>();
            var runLocalArray = new List<bool>();

            foreach (var (playerId, runtimeList) in _playerActiveAbilityInfo)
            {
                foreach (var info in runtimeList)
                {
                    playerIds.Add(playerId);
                    abilityNames.Add((int)info.Instance.AbilityName);
                    isRunningArray.Add(!info.Instance.AfterCooldown);
                    runLocalArray.Add(info.RunLocal);
                }
            }

            RPC_SyncAbilityState(playerIds.ToArray(), abilityNames.ToArray(), isRunningArray.ToArray(), runLocalArray.ToArray());
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestAbility(AbilityContext context)
        {
            TryExecuteAbilityUnified(context, isAuthority: true);
            _abilityStateDirty = true;
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_SyncAbilityState(int[] playerIds, int[] abilityNames, bool[] isRunningArray, bool[] runLocalArray)
        {
            if (Runner.IsServer) return;
            var syncedKeys = new HashSet<(int playerId, AbilityName abilityName)>();

            for (int i = 0; i < playerIds.Length; i++)
            {
                var playerId = playerIds[i];
                var ability = (AbilityName)abilityNames[i];
                var isRunning = isRunningArray[i];
                var runLocal = runLocalArray[i];

                syncedKeys.Add((playerId, ability));

                if (!_playerActiveAbilityInfo.TryGetValue(playerId, out var list))
                {
                    list = new List<AbilityRuntimeInfo>();
                    _playerActiveAbilityInfo[playerId] = list;
                }

                var existing = list.FirstOrDefault(x => x.Instance.AbilityName == ability);

                if (isRunning)
                {
                    if (existing == null)
                    {
                        var refAbility = _abilityReferences.Find(x => x.AbilityName == ability);
                        if (refAbility == null) continue;

                        var instance = refAbility.Clone(refAbility);
                        instance.InitAbility(new AbilityContext { SourcePlayer = playerId }, _spawner);

                        list.Add(new AbilityRuntimeInfo
                        {
                            Instance = instance,
                            RunLocal = runLocal,
                            IsAuthorityInstance = false
                        });
                    }
                }
                else
                {
                    if (existing != null && existing.Instance.CurrentCooldown <= 0f)
                        list.Remove(existing);
                }
            }

            foreach (var kvp in _playerActiveAbilityInfo)
            {
                kvp.Value.RemoveAll(info =>
                    !syncedKeys.Contains((kvp.Key, info.Instance.AbilityName)) && info.Instance.AfterCooldown);
            }
            
        }

        public void ApplyAbilityState(AbilitySharedState abilitySharedState)
        {
            if (!_isInitialized) Initialize();
            RPC_SyncAbilityState(abilitySharedState);
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_SyncAbilityState(AbilitySharedState abilityStates)
        {
            if (Runner.IsServer) return;
            var playerId = abilityStates.OwnerPlayerId;
            if (_playerActiveAbilityInfo.TryGetValue(playerId, out var list))
            {
                //該当アビリティを摘出
                list.FirstOrDefault(x => x.Instance.AbilityName == abilityStates.AbilityName)?.Instance
                    .ApplySharedState(abilityStates);
            }
        }

        public void Register(ServiceLocator locator)
        {
            locator.Register<IAbilityExecutor>(this);
        }
    }

    public class AbilityRuntimeInfo
    {
        public AbilityBase Instance;
        public bool RunLocal;
        public bool IsAuthorityInstance;
    }
}