using System.Collections.Generic;
using Fusion;
using InGame.Health;
using September.Common;
using September.InGame.Effect;
using UnityEngine;
using UnityEngine.AI;

namespace Ingame.Tanihira
{

    /// <summary>
    /// NPCの状態を定義するEnum
    /// </summary>
    public enum FriendState
    {
        None,
        Idle,
        Move,
        Attack,
        Chase,
        Stun,
        Wait
    }
    
    /// <summary>
    /// フレンド機能のベースクラス
    /// </summary>
    public class FriendBase : NetworkBehaviour, IFriendBuff
    {
        [SerializeField] protected Animator _animator;
        [SerializeField] protected Dictionary<FriendState, IFriendState> _friendStateMappings = new Dictionary<FriendState, IFriendState>();
        [SerializeField] protected FriendState _initialState = FriendState.Idle;
        [SerializeField, ReadOnly] protected Transform _destination;
        [SerializeField] protected FriendStatus _friendStatus;
        [SerializeField] protected Transform _formationPos;
        [SerializeField] protected HitChecker _hitChecker;
        [SerializeField, ReadOnly] protected FriendState _currentState;
        [SerializeField] private GameObject _tutankhamen;
        [SerializeField] private Transform _attackEffectPos;
        [SerializeField] private GameObject _runEffectObject;
        [SerializeField] private float _maxRunBlendTreeCount = 5.0f;
        [SerializeField] private GameObject _meshObject;
        [SerializeField] private float _movementBuffScale = 0.3f;

        [Networked] private NetworkBool HasMask { get; set; }
        [Networked] private NetworkBool HasRunEffect { get; set; }
        [Header("攻撃指示")]
        [Networked] public NetworkBool IsAttackOrdered { get;  private set; }
        [Header("攻撃可能")]
        [Networked] public NetworkBool IsCanAttack { get; private set; }
        [Header("規定の攻撃量（超えたら指示があるまで攻撃はしない）")] 
        [SerializeField] private int _regulationAttackAmount;

        [Networked, OnChangedRender(nameof(OnChangeVisible))] public NetworkBool IsVisible { get; set; } = true;
        [Networked, OnChangedRender(nameof(OnChangeScale))] public float Scale { get; set; } = 1f;
        
        protected NavMeshAgent _agent;
        protected NetworkRunner _networkRunner;
        protected NetworkObject _ownerPlayer;
        protected NetworkMecanimAnimator _mecanimAnimator;
        protected FormationManager _formationManager;
        protected FriendState _waitStockState;
        protected FriendStatus _currentStatus;
        protected EffectSpawner _effectSpawner;
        protected bool _isEnd;
        /// <summary>
        /// 現在の攻撃量
        /// </summary>
        private int _currentAttackAmount;
        private int _originalRegulationAttackAmount;

        private static int _spawnCount;
        public bool IsAttack;
        
        
        // プロパティ
        public NavMeshAgent Agent => _agent;
        public Animator Animator => _animator;
        public FormationManager FormationManager => _formationManager;
        public Transform Destination => _destination;
        public NetworkRunner FriendRunner => _networkRunner;
        public Transform FormationPos => _formationPos;
        public FriendStatus CurrentFriendStatus => _currentStatus;
        public FriendState CurrentState => _currentState;
        public NetworkMecanimAnimator MecanimAnimator => _mecanimAnimator;
        public FriendState WaitStockState => _waitStockState;
        public NetworkObject OwnerPlayer => _ownerPlayer;
        public GameObject Tutankhamen => _tutankhamen;
        public bool IsWarp;

        /// <summary>
        /// 攻撃可能な状態か
        /// true：可能
        /// false：不可能
        /// </summary>
        public bool IsAttackPossible => IsCanAttack && IsAttackOrdered;

        public override void Spawned()
        {
            _currentStatus = _friendStatus.Clone();
            //Noneステートの設定
            _friendStateMappings[FriendState.None] = new FriendNoneState();
            _agent = GetComponent<NavMeshAgent>();
            _mecanimAnimator = GetComponent<NetworkMecanimAnimator>();

            _originalRegulationAttackAmount = _regulationAttackAmount;
            
            InitializeStates();
            ChangeState(_initialState);
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;

            if (_formationManager != null && _formationManager.IsPlayerAirborne)
            {
                if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
                    _agent.isStopped = true;

                _animator?.SetFloat("MoveBlend", 0);
                return;
            }
            
            // 現在のステートのUpdateを呼び出し
            _friendStateMappings[_currentState]?.OnUpdate(this);
            if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
            {
                transform.position = _agent.nextPosition;

                //位置を更新する
                if (_agent.remainingDistance >= _agent.stoppingDistance &&
                    _agent.desiredVelocity.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.LookRotation(_agent.velocity);
                }
            }
        }

        /// <summary>
        /// 初期化処理
        /// </summary>
        protected virtual void InitializeStates()
        {
            //ヒット処理
            _hitChecker.OnHit -= OnHitEnemy;
            _hitChecker.OnHit += OnHitEnemy;
            
            //アニメーションのルートモーションを不可
            if (_animator)
            {
                _animator.applyRootMotion = false;
            }
            
            _agent.enabled = false;
            _agent.enabled = true;
            //Agentでの移動を無効かする
            _agent.updatePosition = false;
            _agent.updateRotation = false;
            ApplyStatus();
        }

        /// <summary>
        /// ステートを変更する
        /// </summary>
        /// <param name="newState">新しいステート</param>
        public virtual void ChangeState(FriendState newState)
        {
            if (!HasStateAuthority) return;
            
            if (_isEnd || IsWarp)
                return;
            
            //Noneの時には強制的にステートを変更させる
            if (newState == FriendState.None)
            {
                // 現在のステートのOnExitを呼び出し、コンポーネントを無効化
                _friendStateMappings[_currentState]?.OnExit(this);
                // 新しいステートに変更
                _currentState = newState;
                return;
            }
            else if (newState == FriendState.Attack)
            {
                if (IsAttack)
                    return;
            }
            
            if (_currentState == FriendState.Wait)
            {
                //attackの場合はchaseに変えておく
                if(newState == FriendState.Attack)
                {
                    newState = FriendState.Chase;
                }
                
                _waitStockState = newState;
            }
            
            if (_friendStateMappings[newState] == null)
            {
                Debug.LogWarning($"ステート {newState} が設定されていません");
                return;
            }
            
            // 現在のステートのOnExitを呼び出し、コンポーネントを無効化
            _friendStateMappings[_currentState]?.OnExit(this);
            
            // 新しいステートに変更
            _currentState = newState;
            
            // 新しいステートのOnEnterを呼び出し
            _friendStateMappings[_currentState]?.OnEnter(this);
        }
        
        /// <summary>
        /// 目的地を変更する
        /// </summary>
        /// <param name="destination"></param>
        public void SetDestination(Transform destination)
        {
            _destination = destination;
        }
        
        /// <summary>
        /// FormationManagerを設定
        /// </summary>
        /// <param name="formationManager">FormationManager</param>
        public void SetFormationManager(FormationManager formationManager)
        {
            _formationManager = formationManager;
        }

        /// <summary>
        /// オーナープレイヤーを設定
        /// </summary>
        /// <param name="ownerPlayer">オーナープレイヤー</param>
        public void SetOwnerPlayer(NetworkObject ownerPlayer)
        {
            _ownerPlayer = ownerPlayer;
        }
        
        private void OnHitEnemy(Collider hitInfo)
        {
            if (!HasStateAuthority) return;
            var targetObj = hitInfo.GetComponentInParent<NetworkObject>();
            if (targetObj == null || targetObj == _ownerPlayer) return;
            //隊列の味方も除外
            if (targetObj.TryGetComponent<FriendBase>(out var friend))
            {
                if (_formationManager != null && _formationManager.FriendsList != null && _formationManager.FriendsList.Contains(friend))
                    return;
            }
            
            var damageable = hitInfo.GetComponentInParent<IDamageable>();
            if (damageable == null) return;
            var hitData = new HitData(
                HitActionType.Damage,
                _currentStatus.AttackPower,
                _ownerPlayer.InputAuthority,
                damageable.OwnerPlayerRef);
            damageable.TakeHit(ref hitData);
            //エフェクトを出す
            PlayEffect();
            
            // 現在の攻撃量に攻撃力分増加させる
            _currentAttackAmount += _currentStatus.AttackPower;
            if (_currentAttackAmount >= _regulationAttackAmount)
            {
                // 規定量を超えたら攻撃を辞め、プレイヤーの元に帰還する
                IsCanAttack = false;
                IsAttackOrdered = false;
                SetDestination(OwnerPlayer.transform);
                ChangeState(FriendState.Move);
                if (OwnerPlayer.TryGetComponent<FriendPlayerDetector>(out var detector))
                {
                    // 全ペンギンの攻撃を止めにする
                    detector.ChangeDetectionCenter(OwnerPlayer.transform, true);
                    if (OwnerPlayer.TryGetComponent<FormationManager>(out var formationManager))
                    {
                        foreach (var fr in formationManager.CurrentFriendsList)
                        {
                            fr.IsCanAttack = false;
                            fr.IsAttackOrdered = false;
                        }
                    }
                }
            }
        }
        
        private void PlayEffect()
        {
            // Effect生成処理
            _effectSpawner ??= StaticServiceLocator.Instance.Get<EffectSpawner>();
            _effectSpawner?.RequestPlayOneShotEffect(EffectType.PenguinAttack, _attackEffectPos.position,
                _attackEffectPos.rotation);
            // 音量再生
            //CRIAudio.PlaySE("ALLCue", SoundCues.SE.Penguin_Attack);
        }

        public void FinishWaitTime()
        {
            if (_waitStockState == FriendState.None)
            {
                ChangeState(FriendState.Move);
            }
            else
            {
                ChangeState(_waitStockState);
                _waitStockState = FriendState.None;
            }
        }

        public void EndFriend()
        {
            _isEnd = true;
            // 現在のステートのOnExitを呼び出し、コンポーネントを無効化
            _friendStateMappings[_currentState]?.OnExit(this);
            // 新しいステートに変更
            _currentState = FriendState.None;
        }

        // アニメーションイベントから呼び出されます
        public void StartAttack()
        {
            IsAttack = true;
            _hitChecker.StartHitCheck();
        }

        // アニメーションイベントから呼び出されます
        public void EndAttack()
        {
            IsAttack = false;
            _hitChecker.EndHitCheck();
            ChangeState(FriendState.Chase);
        }

        public override void Render()
        {
            if(_tutankhamen)
                _tutankhamen.SetActive(HasMask);
            if(_runEffectObject)
                _runEffectObject.SetActive(HasRunEffect);
        }
        public void SetMask(bool value)
        {
            if (!HasStateAuthority) return;
            HasMask = value;
        }

        private void SetRunEffect(bool value)
        {
            if (!HasStateAuthority) return;
            HasRunEffect = value;
        }

        public void ChangeRunEffect(float value)
        {
            if (value >= _maxRunBlendTreeCount)
            {
                SetRunEffect(true);
            }
            else
            {
                SetRunEffect(false);
            }
        }

        public void StartBuff(float speedBuffRate)
        {
            _currentStatus.FriendFormationSpeed *= speedBuffRate * _movementBuffScale;
            _currentStatus.FriendChaseSpeed *= speedBuffRate * _movementBuffScale;
            _currentStatus.FriendRotateSpeed *= speedBuffRate * _movementBuffScale;
            _currentStatus.FriendAcceleration *= speedBuffRate * _movementBuffScale;
            _regulationAttackAmount = Mathf.FloorToInt(speedBuffRate * _regulationAttackAmount);
            _currentStatus.AttackPower = (int)(_currentStatus.AttackPower * speedBuffRate);
            ApplyStatus();
        }

        public void StopBuff()
        {
            _currentStatus.FriendFormationSpeed = _friendStatus.FriendFormationSpeed;
            _currentStatus.FriendChaseSpeed = _friendStatus.FriendChaseSpeed;
            _currentStatus.FriendRotateSpeed = _friendStatus.FriendRotateSpeed;
            _currentStatus.FriendAcceleration = _friendStatus.FriendAcceleration;
            _regulationAttackAmount = _originalRegulationAttackAmount;
            _currentStatus.AttackPower = _friendStatus.AttackPower;
            ApplyStatus();
        }

        /// <summary>
        /// 現在のステータスをAgentに反映する
        /// </summary>
        private void ApplyStatus()
        {
            //現在のステートによってスピードを反映する
            switch (_currentState)
            {
                case FriendState.Chase:
                    _agent.speed = _currentStatus.FriendChaseSpeed;
                    break;
                default:
                    _agent.speed = _currentStatus.FriendFormationSpeed;
                    break;
            }

            _agent.angularSpeed = _currentStatus.FriendRotateSpeed;
            _agent.acceleration = _currentStatus.FriendAcceleration;
        }

        /// <summary>
        /// 現在の攻撃量、フラグをリセットする
        /// </summary>
        public void ResetAttackAmount()
        {
            _currentAttackAmount = 0;
            IsCanAttack = true;
            SetAttackOrdered(false);
        }

        /// <summary>
        /// 攻撃指示状態を設定
        /// </summary>
        /// <param name="isOrdered">攻撃指示を行う場合はtrue</param>
        public void SetAttackOrdered(bool isOrdered)
        {
            IsAttackOrdered = isOrdered;
        }

        private void OnChangeVisible()
        {
            _agent.enabled = IsVisible;
            _meshObject.SetActive(IsVisible);
        }

        private void OnChangeScale()
        {
            transform.localScale = Vector3.one * Scale;
        }
    }
}
