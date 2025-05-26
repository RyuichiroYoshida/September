using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Fusion;
using NaughtyAttributes;
using UnityEngine;

namespace InGame.Player.Ability
{
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
            list.RemoveAll(x => x.Instance.AbilityName == context.AbilityName);
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
        private readonly StringBuilder _stringBuilder = new StringBuilder();
        private void Update()
        {
            // foreach (var playerRef in PlayerActiveAbilityInfo.Keys)
            // {
            //     if (playerRef == Runner.LocalPlayer)
            //     {
            //         _stringBuilder.AppendLine($"Player {playerRef.PlayerId}");
            //         foreach (var ability in PlayerActiveAbilityInfo[playerRef])
            //         {
            //             var abilityName = ability.Instance.AbilityName.ToString();
            //             var time = ability.Instance.CurrentCooldown;
            //             var maxTime = ability.Instance.Cooldown;
            //             _stringBuilder.AppendLine($"Ability: {abilityName} Cooldown: {time:F1}/{maxTime:F1}");
            //         }
            //     }
            // }
            //Debug.Log(_stringBuilder.ToString());
        }
        
        public override void Spawned()
        {
            Debug.Log($"AbilityExecutor Spawned | HasStateAuthority={HasStateAuthority}, HasInputAuthority={HasInputAuthority}");
            Debug.Log($"IsSimulationBehaviour: {Runner.SetIsSimulated(this.GetComponent<NetworkObject>(), true)}");
            Debug.Log("CanReceiveSimulationCallback" + CanReceiveSimulationCallback);
        }

        public override void FixedUpdateNetwork()
        {
            Debug.Log("AbilityExecutor FixedUpdateNetwork");
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

    public class AbilityRuntimeInfo
    {
        public AbilityBase Instance;
        public bool IsAuthorityInstance;
    }

    [Serializable]
    public abstract class AbilityBase
    {
        public enum AbilityPhase
        {
            None,
            Started,
            Active,
            Ending,
            Ended
        }

        [SerializeField] private AbilityName _abilityName;
        [SerializeField] protected float _cooldown;
        protected AbilityPhase _phase = AbilityPhase.None;
        protected ISpawner _spawner;
        public event Action OnEndAbilityEvent;
        public float Cooldown => _cooldown;
        public float CurrentCooldown { get; protected set; }
        public virtual string DisplayName => AbilityName.ToString();
        public AbilityName AbilityName => _abilityName;
        public AbilityContext Context { get; private set; }

        protected AbilityBase() {}
        protected AbilityBase(AbilityBase abilityReference)
        {
            _abilityName = abilityReference._abilityName;
            _cooldown = abilityReference._cooldown;
        }

        protected bool IsCooldown => CurrentCooldown > 0f;

        /// <summary>
        /// ホストとクライアントで共有する変数の計算を行う
        /// 例えばクールタイムはクライアント側でも現在の値を参照したいのでここに書く
        /// </summary>
        /// <param name="deltaTime"></param>
        public virtual void CalculateSharedVariable(float deltaTime)
        {
            if (CurrentCooldown > 0f) CurrentCooldown -= deltaTime;
            if (CurrentCooldown < 0f) CurrentCooldown = 0f;
        }
        
        public virtual void ResetSharedVariable()
        {
            CurrentCooldown = _cooldown;
        }

        public abstract AbilityBase Clone(AbilityBase abilityReference);

        
        /// <summary>
        /// ここには初期化処理だけ書いてください
        /// 実際のアビリティの挙動はStartかUpdateにお願いします
        /// </summary>
        /// <param name="context"></param>
        /// <param name="spawner"></param>
        public virtual void InitAbility(AbilityContext context, ISpawner spawner)
        {
            Context = context;
            _spawner = spawner;
        }

        public void Tick(float deltaTime)
        {
            switch (_phase)
            {
                case AbilityPhase.None: break;
                case AbilityPhase.Started:
                    OnStart();
                    _phase = AbilityPhase.Active;
                    break;
                case AbilityPhase.Active:
                    OnUpdate(deltaTime);
                    break;
                case AbilityPhase.Ended:
                    EndAbility();
                    break;
            }
        }

        protected virtual void OnStart() {}

        public virtual bool TryInitializeWithTrigger(AbilityContext context,
            List<AbilityRuntimeInfo> currentPlayerActiveAbilityInfo, ISpawner spawner)
        {
            switch (context.ActionType)
            {
                case AbilityActionType.発動:
                    if (currentPlayerActiveAbilityInfo == null || currentPlayerActiveAbilityInfo.All(x => x.Instance.AbilityName != AbilityName))
                    {
                        InitAbility(context, spawner);
                        _phase = AbilityPhase.Started;
                        return true;
                    }
                    break;
                case AbilityActionType.停止:
                    var currentRunningAbility = currentPlayerActiveAbilityInfo.FirstOrDefault(x => x.Instance.AbilityName == AbilityName);
                    if (currentRunningAbility != null)
                    {
                        currentRunningAbility.Instance.ForceEnd();
                        _phase = AbilityPhase.Ending;
                    }
                    break;
            }
            return false;
        }

        protected virtual void OnUpdate(float deltaTime) {}
        public virtual void ForceEnd() => _phase = AbilityPhase.Ended;

        public virtual void OnEndAbility() {}

        protected void EndAbility()
        {
            OnEndAbility();
            OnEndAbilityEvent?.Invoke();
        }
    }

    [Serializable]
    public class AbilityGenerateFloor : AbilityBase
    {
        [SerializeField, Label("生成する床")] private GameObject _floorPrefab;
        private int _spawnObjId = -1;
        private string _floorPrefabGuid = "3eaa7436e4c04084c89c99e380ed4063";

        public override string DisplayName => "床生成";

        public AbilityGenerateFloor() {}
        public AbilityGenerateFloor(AbilityBase abilityReference) : base(abilityReference)
        {
            _floorPrefab = ((AbilityGenerateFloor)abilityReference)._floorPrefab;
        }

        public override AbilityBase Clone(AbilityBase abilityBase) => new AbilityGenerateFloor(this);

        protected override void OnStart()
        {
            var player = GameObject.FindObjectsByType<PlayerAvatar>(FindObjectsSortMode.None).FirstOrDefault(x => x.PlayerRef == Context.SourcePlayer);
            var pos = player ? player.transform.position : Vector3.zero;
            var rot = Quaternion.Euler(player ? player.transform.forward : Vector3.zero);
            _spawnObjId = _spawner.Spawn(_floorPrefabGuid, pos, rot);
        }

        protected override void OnUpdate(float deltaTime)
        {
            if (!IsCooldown) ForceEnd();
        }

        public override void OnEndAbility()
        {
            if (_spawnObjId != -1 && _spawner != null)
            {
                _spawner.Despawn(_spawnObjId);
                _spawnObjId = -1;
            }
        }
    }
}
