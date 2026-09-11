using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Fusion;
using InGame.Health;
using InGame.Interact;
using InGame.Player;
using Ingame.Tanihira;
using NaughtyAttributes;
using September.Common;
using September.InGame.Common;
using September.InGame.Effect;
using September.InGame.Mountable;
using September.InGame.UI;
using UnityEngine;

namespace InGame.Exhibit
{
    public class MountableExhibitBase : NetworkBehaviour, IDamageable , IMountable
    {
        protected CameraController CameraController { get; private set; }
        
        protected Animator Animator { get; private set; }

        protected Rigidbody Rigidbody { get; private set; }
        
       // protected Action HitAction { get; set; }

        protected bool IsSpawned { get; private set; }
        
        #region AttackParam

        protected MeleeHitboxExecutor Executor;
        [SerializeField] private List<Transform> _points;
        [SerializeField] private float _hitboxRadius = 0.2f;
        [SerializeField] private LayerMask _hitMask;
        [SerializeField] private int _startFrame;
        [SerializeField] private int _endFrame = 34;
        [SerializeField] private int _damageAmount = 100;
        protected float StartFrame => _startFrame;
        protected float EndFrame => _endFrame;
        #endregion
        
        #region DamageableParam
        public bool IsAlive =>CurrentHealth > 0;
        public PlayerRef OwnerPlayerRef => Object.InputAuthority;

        private bool _isInvincible ;
    
        public int CurrentHealth;
        
        private Vector3 _initialPosition;
        private Quaternion _initialRotation;
    
        [SerializeField] private int _maxHealth;
        
        #endregion
        
        private PlayerManager _ownerPlayerManager;
        
        [SerializeField, Label("Playerが登場する位置")] private Transform _getOffPoint;
        
        private Collider _collider;

        [Label("Playerに戻るときの地面からの高さ")][SerializeField] private float _height = 10f;
        EffectSpawner _effectSpawner;
        [SerializeField] private InteractableBase _interactable;
        private InGameManager _inGameManager;
        public bool IsEnding;
        private CancellationTokenSource _cts;
        [SerializeField,Label("降りた後の無敵時間")] private float _invincibleTime;
        public override void Spawned()
        {
             IsEnding = false;
             Rigidbody = GetComponent<Rigidbody>();
             if (TryGetComponent(out CameraController cameraController))
             {
                 CameraController = cameraController;
                 cameraController.Init(true);
             }
             Animator = GetComponent<Animator>();
             Rigidbody.isKinematic = true;
             CurrentHealth = _maxHealth;
             IsSpawned = true;
             _initialPosition = transform.position;
             _initialRotation = transform.rotation;
             _collider = gameObject.GetComponentInHierarchy<Collider>();
             if (!_effectSpawner)
                 _effectSpawner = StaticServiceLocator.Instance.Get<EffectSpawner>();
             if(!Runner.IsServer) return;
             _inGameManager = StaticServiceLocator.Instance.Get<InGameManager>();
             _inGameManager.GameEnded += () => {IsEnding = true;};
             RPC_SetIsKinematic(true);
        }
        
        protected void CreateHitBox(PlayerRef playerRef)
        {
            Executor = new MeleeHitboxExecutor(_points, _hitboxRadius, _hitMask, _startFrame, _endFrame)
            {
                OnHit = (col,pos) =>
                {
                    var damageable = col.GetComponentInParent<IDamageable>();
                    if(col == _collider) return;
                    if (damageable == null) return;
                    var hitData = new HitData(HitActionType.Damage, _damageAmount, playerRef,
                        damageable.OwnerPlayerRef);
                    damageable.TakeHit(ref hitData);
                    _effectSpawner.RequestPlayOneShotEffect(EffectType.HitNormal,col.ClosestPoint(pos), Quaternion.identity);
                }
            };
        }
        
        /// <summary>
        /// 展示物に乗ってる間のUpdate関数
        /// </summary>
        public virtual void OnInteractFixedUpdate(PlayerInput playerInput,float deltaTime)
        {
            if(_ownerPlayerManager == null) return;
            _ownerPlayerManager.transform.position = transform.position;
        }
        
        private void LateUpdate()
        {
            if(!IsSpawned || !HasInputAuthority) return;
            
            if (GameInput.I.Player.Aim.triggered)
            {
                CameraController.CameraReset();
            }
            CameraController.RotateCamera(GameInput.I.Player.Look.ReadValue<Vector2>(),Runner.DeltaTime);
        }
        
        /// <summary>
        /// インタラクト開始時の切り替え処理
        /// ホストでのみ実行される点に注意
        /// </summary>
        public virtual void GetOn(PlayerRef playerRef)
        {
            _cts = new CancellationTokenSource();
            CameraController.CameraReset();
            _interactable.LastUsedCooldownTime = -9999f;
            CurrentHealth = _maxHealth;
            var obj = StaticServiceLocator.Instance.Get<InGameManager>()
                .PlayerDataDic[playerRef];
            _ownerPlayerManager = obj.GetComponent<PlayerManager>();
            _ownerPlayerManager.SetControlState(PlayerManager.PlayerControlState.ForcedControl);
            _ownerPlayerManager.RPC_SetUseGrav(false);
            _ownerPlayerManager.RPC_SetColliderActive(false);
            _ownerPlayerManager.RPC_SetMeshActive(false);
            Object.AssignInputAuthority(playerRef);
            RPC_ChangeDescriptionUI(playerRef, ControlDescriptionType.Exhibit);
            CameraController.Init(true);
            RPC_SetCameraPriority(playerRef,15);
            RPC_SetIsKinematic(false);
            if (_ownerPlayerManager.TryGetComponent<FormationManager>(out var formationManager))
            {
                formationManager.WarpFriendOutField();
            }
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_ChangeDescriptionUI(PlayerRef target, ControlDescriptionType mode)
        {
            if (Runner.LocalPlayer == target)
            {
                UIController.I.ChangeDescriptionUI(mode);
            }
        }
        
        /// <summary>
        /// インタラクト終了時の切り替え処理
        /// ホストでのみ実行される点に注意
        /// </summary>
        public virtual void GetOff(PlayerRef playerRef)
        {
            Vector3 getOffPosition = _getOffPoint != null
                ? _getOffPoint.position
                : _ownerPlayerManager.transform.position;
            Quaternion getOffRotation = _getOffPoint != null
                ? _getOffPoint.rotation
                : _ownerPlayerManager.transform.rotation;

            PlayerDatabase.Instance.PlayerDataDic.TryGet(playerRef, out var playerData);
            ControlDescriptionType type = CharacterDataContainer.Instance.GetControlDescriptionType(playerData.CharacterType);
            
            // UIの切り替え
            RPC_ChangeDescriptionUI(playerRef, type);
            
            // インタラクト物を基の位置に移動
            transform.SetPositionAndRotation(_initialPosition, _initialRotation);
            
            // クールダウン処理
            var chara = PlayerDatabase.Instance.PlayerDataDic[playerRef].CharacterType;
            var time = _interactable.CooldownTimeDictionary.Dictionary.TryGetValue(CharacterType.All, out var all)
                ? all
                : _interactable.CooldownTimeDictionary.Dictionary.GetValueOrDefault(chara, 0f);
            _interactable.SetCooldown(time);
            
            // プレイヤーの状態復帰
            _ownerPlayerManager.SetControlState(PlayerManager.PlayerControlState.Normal);
            _ownerPlayerManager.RPC_SetUseGrav(true);
            _ownerPlayerManager.RPC_SetColliderActive(true);
            _ownerPlayerManager.RPC_SetMeshActive(true);
            _ownerPlayerManager.transform.SetPositionAndRotation(getOffPosition, getOffRotation);
            var health = _ownerPlayerManager.GetComponent<PlayerHealth>();
            SetInvincible(health,true).Forget();
            Object.RemoveInputAuthority();
            RPC_SetCameraPriority(playerRef,5);
            RPC_SetIsKinematic(true);
            if (_ownerPlayerManager.TryGetComponent<FormationManager>(out var formationManager))
            {
                formationManager.WarpFriendNearPlayerWhenGrounded(
                    _ownerPlayerManager.GetComponent<PlayerMovement>());
            }
            Executor = null;
            CameraController.CameraReset();
        }
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_SetCameraPriority(PlayerRef player,int priority)
        {
            if (Runner.LocalPlayer == player)
            {
                CameraController.SetCameraPriority(priority);
            }
        }
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_SetIsKinematic(bool kinematic)
        {
           Rigidbody.isKinematic = kinematic;
        }
        
        
        public void TakeHit(ref HitData hitData)
        {
            ApplyHit(ref hitData);

            hitData.IsLastHit = !IsAlive;

            if (HasStateAuthority)
            {
                hitData.Executor?.HitExecution(hitData);
            }
        }

        void ApplyHit(ref HitData hitData)
        {
            if (!IsAlive)
            {
                hitData.Amount = 0;
                return;
            }
            if (hitData.HitActionType.IsDamage())
            {
                hitData.Amount = TakeDamage(hitData.Amount);
                //HitAction?.Invoke();
            }
            else if (hitData.HitActionType == HitActionType.Heal)
            {
                hitData.Amount = TakeHeal(hitData.Amount);
            }
        }
        int TakeDamage(int damage)
        {
            if (_isInvincible) return 0;
            var prevHealth = CurrentHealth;
            CurrentHealth = Mathf.Clamp(CurrentHealth - damage, 0, _maxHealth);
            return prevHealth - CurrentHealth;
        }

        int TakeHeal(int heal)
        {
            if (_isInvincible) return 0;
            var prevHealth = CurrentHealth;
            CurrentHealth = Mathf.Clamp(CurrentHealth + heal, 0, _maxHealth);
            return prevHealth - CurrentHealth;
        }

        private async UniTaskVoid SetInvincible(PlayerHealth playerHealth,bool isInvincible)
        {
            if(!HasStateAuthority) return; 
            playerHealth.IsInvincible = isInvincible;
            await UniTask.WaitForSeconds(_invincibleTime,cancellationToken: _cts.Token); // 展示物から降りて〇秒間は無敵
            playerHealth.IsInvincible = !isInvincible;
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
    }
}
