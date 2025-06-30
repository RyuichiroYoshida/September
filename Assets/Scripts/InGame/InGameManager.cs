using System.Collections.Generic;
using System.Threading;
using Fusion;
using NaughtyAttributes;
using September.Common;
using September.InGame.UI;
using UnityEngine;

namespace September.InGame.Common
{
    public class InGameManager : NetworkStateMachineOwner<InGameManager>, IRegisterableService
    {
        [Header("Timer Settings"), SerializeField, Label("TimerData")]
        
        private GameTimerData _timerData;

        [Header("他Playerを気絶させたときに得られるスコア"), SerializeField] 
        private int _addScore;

        private readonly Dictionary<PlayerRef, NetworkObject> _playerDataDic = new();

        private NetworkRunner _networkRunner;
        public IReadOnlyDictionary<PlayerRef, NetworkObject> PlayerDataDic => _playerDataDic;
        public GameTimerData TimerData => _timerData;
        public CancellationTokenSource Cts { get; private set; }

        public int AddScore => _addScore;

        public void Register(ServiceLocator locator)
        {
            locator.Register(this);
        }

        public override void Spawned()
        {
            Cts = new CancellationTokenSource();
            _networkRunner = FindFirstObjectByType<NetworkRunner>();
            if (_networkRunner == null) Debug.LogError("NetworkRunnerがありません");
            base.Spawned();
        }

        protected override void InitializeStateMachine()
        {
            //  ステートマシン初期設定
            _stateMachine.AddTransition<PreparationState, PlayingState>((int)StateEventId.Finish);
            _stateMachine.AddTransition<PlayingState, EndingState>((int)StateEventId.Finish);
            _stateMachine.SetStartState<PreparationState>();
        }

        public void AddPlayerObject(PlayerRef playerRef, NetworkObject networkObject)
        {
            _playerDataDic.Add(playerRef, networkObject);
        }
    }
}