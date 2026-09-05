using System.Collections.Generic;
using DG.Tweening;
using Fusion;
using InGame.Common;
using InGame.Health;
using InGame.Interact;
using InGame.Player;
using Ingame.Tanihira;
using September.Common;
using September.InGame.Common;
using TMPro;
using UnityEngine;
using PlayerInput = September.Common.PlayerInput;
using CRISound;
using September.InGame.UI;

namespace InGame.Exhibit
{
    public class PropAirplane : InteractableBase
    {
        [Header("Ride")] [SerializeField] private Transform _getOffPoint;
        [Header("Flight")] [SerializeField] private float _grav;
        [SerializeField] private Vector3 _drag;
        [SerializeField] private float _jerk;
        [SerializeField] private float _propDrag;
        [SerializeField] private float _maxAccel;
        [SerializeField] private float _lift;
        [SerializeField, Tooltip("重力を打ち消す速度")] private float _forwardSpeedBalancedByGravity;
        [SerializeField] private GameObject _wheelObj;
        [Header("Rotate")] [SerializeField] private float _angularDrag;
        [SerializeField] private float _rotSpeedPitch;
        [SerializeField] private float _rotSpeedRoll;
        [SerializeField] private float _rotSpeedYaw;
        [SerializeField] private float _rotReturnSpeedRoll;
        [Header("Ground")] [SerializeField] private Vector3 _groundDrag;
        [SerializeField] private float _angularGroundDrag;
        [SerializeField] private float _rotSpeedGroundYaw;
        [Header("Prop")] [SerializeField] private Transform _prop;
        [SerializeField] private float _propSpeedRate;

        [Header("MachineGun")] [SerializeField]
        private float _fireInterval;
        [SerializeField] private Transform[] _muzzles;
        [SerializeField] private Transform _firePoint;
        [SerializeField] private ParticleSystem _muzzleFlash;
        [SerializeField] private ParticleSystem _ballistic;
        [SerializeField] private ParticleSystem _bulletMark;
        [SerializeField] private int _damageAmount;
        [SerializeField] private float _gunMaxDistance;
        [SerializeField] private LayerMask _shootingTargetLayerMask = ~0;
        [SerializeField] private LayerMask _bulletHitLayerMask = ~0;
        [SerializeField] private float _castRadius;
        [Header("Debug")] [SerializeField] private TMP_Text _velocityText;
        [SerializeField] private TMP_Text _forwardSpeedText;
        [SerializeField] private TMP_Text _currentAccelText;
        [SerializeField] private TMP_Text _angleText;
        [SerializeField] private TMP_Text _isUpText;

        private Rigidbody _rb;
        private AirplaneCamera _cameraController;

        private PlayerManager _ownerPlayerManager;

        // move
        private bool _onGround;
        private bool _onGroundWheel;
        private Vector3 _groundNormal;

        private bool IsGround => _onGroundWheel;

        // FixedUpdateNetwork で AddForce するときの補正
        private float PhysicsCoefficient => Runner.DeltaTime / Time.fixedDeltaTime;

        private bool _sendToHost;

        // MachineGun
        private float _machineGunTimer;

        [Networked, OnChangedRender(nameof(OnChangeOwnerPlayerRef))]
        private PlayerRef OwnerPlayerRef { get; set; }

        [Networked] private float CurrentAccel { get; set; }
        [Networked] private NetworkButtons PreviousButtons { get; set; }
        [Networked] private float GetOnTime { get; set; }

        private Vector3 _initialPosition;
        private Quaternion _initialRotation;
        private float _interactTimer;

        [SerializeField] private float _interactTime;
        
        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _cameraController = GetComponent<AirplaneCamera>();
        }

        [SerializeField] private bool _isSpawned = false;

        private bool _isEnd;

        public override void Spawned()
        {
            _isSpawned = false;
            _isSpawned = true;
            // 初期位置・回転を記録
            _initialPosition = transform.position;
            _initialRotation = transform.rotation;
            if (!Runner.IsServer) return;
            StaticServiceLocator.Instance.Get<InGameManager>().GameEnded += () => _isEnd = true;
            RPC_SetIsKinematic(true);
        }

        public override void FixedUpdateNetwork()
        {
            if (HasStateAuthority)
            {
                if (GetInput<PlayerInput>(out var input))
                {
                    _interactTimer += Time.fixedDeltaTime;
                    
                    if (_isEnd)
                    {
                        GetOff();
                        return;
                    }
                    
                    if (CheckInteractEnd())
                    {
                        GetOff();
                        return;
                    }
                    
                    // 乗ってから1秒は降りる処理を無視（ティラノと同じ仕様）
                    float timeSinceGetOn = Runner.SimulationTime - GetOnTime;
                    
                    // Eキーで降りる（WasPressedで新規押下のみ検知）
                    if (timeSinceGetOn > 1f && input.Buttons.WasPressed(PreviousButtons, PlayerButtons.Interact))
                    {
                        GetOff();
                        return;
                    }
                    
                    // 新しい飛行機専用ボタンを使用
                    if (input.Buttons.IsSet(PlayerButtons.AirPlaneBack))
                    {
                        if (IsGround) AddSpeedBack();
                        else PropSlowDown();
                    }
                    else if (input.Buttons.IsSet(PlayerButtons.AirplaneForward))
                    {
                        AddSpeedForward();
                    }
                    else
                    {
                        PropSlowDown(); // どちらも押されていない場合は減速
                    }

                    RotatePlane(input.MoveDirection);
                    // playerオブジェクトのpositionを固定する
                    _ownerPlayerManager.transform.position = transform.position;

                    UpdateMachineGun(input.Buttons.IsSet(PlayerButtons.Attack), Runner.DeltaTime);

                    // 前フレームのボタン状態を保存
                    PreviousButtons = input.Buttons;
                }
                else
                {
                    PropSlowDown();
                }

                // apply accel
                _rb.AddForce(CurrentAccel * PhysicsCoefficient * transform.forward, ForceMode.Acceleration);

                //ApplyLift();
                CounteractGravity();
                ApplyExternalForces();

                _onGround = false;
                _onGroundWheel = false;
            }
        }
        
        private bool CheckInteractEnd()
        {
            return _interactTimer > _interactTime;
        }

        void AddSpeedForward()
        {
            CurrentAccel = Mathf.Min(CurrentAccel + _jerk * Runner.DeltaTime, _maxAccel);
        }

        /// <summary> プロペラの抵抗減速 </summary>
        void PropSlowDown()
        {
            if (Mathf.Abs(CurrentAccel) > _propDrag * Runner.DeltaTime)
            {
                CurrentAccel += _propDrag * -Mathf.Sign(CurrentAccel) * Runner.DeltaTime;
            }
            else
            {
                CurrentAccel = 0;
            }
        }

        void AddSpeedBack()
        {
            CurrentAccel = Mathf.Max(CurrentAccel - _jerk * Runner.DeltaTime, -_maxAccel);
        }

        void ApplyLift()
        {
            float forwardSpeed = Vector3.Dot(_rb.linearVelocity, transform.forward);
            _rb.AddForce(_lift * forwardSpeed * PhysicsCoefficient * transform.up, ForceMode.Acceleration);
        }

        void CounteractGravity()
        {
            float forwardSpeed = Vector3.Dot(_rb.linearVelocity, transform.forward);
            _rb.AddForce(
                _grav * Mathf.Clamp01(forwardSpeed / _forwardSpeedBalancedByGravity) * PhysicsCoefficient * Vector3.up,
                ForceMode.Acceleration);
        }

        /// <summary> 外部的なAddForce </summary>
        void ApplyExternalForces()
        {
            // grav
            _rb.AddForce(_grav * PhysicsCoefficient * Vector3.down, ForceMode.Acceleration);
            // drag
            if (_rb.linearVelocity.sqrMagnitude > 0.0001f)
            {
                Vector3 dragLocalVelocity = Vector3.Scale(transform.InverseTransformDirection(_rb.linearVelocity),
                    IsGround ? _groundDrag : _drag);
                Vector3 dragWorldVelocity = transform.TransformDirection(dragLocalVelocity);
                _rb.AddForce(_rb.linearVelocity.magnitude * PhysicsCoefficient * -dragWorldVelocity,
                    ForceMode.Acceleration);
            }

            // angular drag
            if (_rb.angularVelocity.sqrMagnitude > 0.0001f)
                _rb.AddTorque(
                    (IsGround ? _angularGroundDrag : _angularDrag) * _rb.angularVelocity.magnitude *
                    PhysicsCoefficient * -_rb.angularVelocity, ForceMode.Acceleration);
        }

        void RotatePlane(Vector2 moveDir)
        {
            float forwardSpeed = Vector3.Dot(_rb.linearVelocity, transform.forward);
            bool isUp = Vector3.Angle(transform.up, Vector3.up) <= 90f;

            if (IsGround)
            {
                Vector3 torque = Vector3.zero;
                torque.y = moveDir.x * _rotSpeedYaw * forwardSpeed;
                torque = transform.TransformDirection(torque);
                _rb.AddTorque(torque * PhysicsCoefficient, ForceMode.Acceleration);
            }
            else
            {
                Vector3 torque = Vector3.zero;
                torque.x = moveDir.y * _rotSpeedPitch * forwardSpeed;

                float eulerZ = transform.eulerAngles.z;

                if (moveDir.x == 0)
                {
                    if (isUp)
                    {
                        torque.z = _rotSpeedRoll * forwardSpeed * Mathf.DeltaAngle(eulerZ, 0) / 90;
                    }
                    else
                    {
                        torque.z = _rotSpeedRoll * forwardSpeed * Mathf.DeltaAngle(eulerZ, 180) / 90;
                    }
                }
                else if (moveDir.x < 0 && Mathf.Abs(80 - eulerZ) > 10)
                {
                    torque.z = moveDir.x * _rotSpeedRoll * forwardSpeed * (isUp ? -1 : 1) *
                               Mathf.Abs(Mathf.DeltaAngle(eulerZ, 80) / 90);
                }
                else if (moveDir.x > 0 && Mathf.Abs(280 - eulerZ) > 10)
                {
                    torque.z = moveDir.x * _rotSpeedRoll * forwardSpeed * (isUp ? -1 : 1) *
                               Mathf.Abs(Mathf.DeltaAngle(eulerZ, 280) / 90);
                }

                torque = transform.TransformDirection(torque);
                // yaw は world 回転
                torque.y += moveDir.x * _rotSpeedYaw * forwardSpeed;
                //torque += moveDir.x * _rotSpeedYaw * forwardSpeed * (Quaternion.Euler(transform.eulerAngles.x, 0, 0) * Vector3.up);
                _rb.AddTorque(torque * PhysicsCoefficient, ForceMode.Acceleration);
            }
        }

        void UpdateMachineGun(bool fireInput, float deltaTime)
        {
            if (fireInput && _machineGunTimer <= 0)
            {
                AudioBroadcaster.RPC_PlaySoundFromCode(SoundCues.SE.ZeroFighter_TakeoffGunFire.Name, SoundTrackingType.Spot, Object); // 射撃音
                Fire();
            }
            else if (_machineGunTimer > 0)
            {
                _machineGunTimer -= deltaTime;
            }
        }

        void Fire()
        {
            _machineGunTimer = _fireInterval;
            var hits = new RaycastHit[20];
            var num = Physics.SphereCastNonAlloc(_firePoint.position, _castRadius, transform.forward, hits,
                _gunMaxDistance, _shootingTargetLayerMask);
            for (int i = 0; i < num; i++)
            {
                if(hits[i].point == Vector3.zero) continue;
                if (!hits[i].collider.transform.root.gameObject
                        .TryGetComponent<IDamageable>(out var damageable)) continue;
                var direction =  hits[i].point - _firePoint.position;
                var ray = new Ray(_firePoint.position, direction);
                if(!Physics.Raycast(ray,out var info) || info.transform !=  hits[i].transform) continue;
                var hitData = new HitData(HitActionType.RangedDamage, _damageAmount, OwnerPlayerRef, damageable.OwnerPlayerRef, null,
                    damageable);
                damageable.TakeHit(ref hitData);
                foreach (var muzzle in _muzzles)
                {
                    RPC_PlayEffect(muzzle.position, hits[i].point);
                }
                return;
            }

            foreach (var muzzle in _muzzles)
            {
                var cast = Physics.Raycast(muzzle.position, transform.forward, out var info, _gunMaxDistance, _bulletHitLayerMask);
                RPC_PlayEffect(muzzle.position, info.point);
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_PlayEffect(Vector3 muzzlePos, Vector3 targetPos)
        {
            ParticlePool.Play(_muzzleFlash, muzzlePos, transform.rotation);
            ParticlePool.Play(_ballistic, muzzlePos, transform.rotation,
                    0.03f * Vector3.Distance(muzzlePos, targetPos))
                .transform.DOMove(targetPos, 0.03f * Vector3.Distance(muzzlePos, targetPos));
            ParticlePool.Play(_bulletMark, targetPos, transform.rotation);
        }

        void GetOn(PlayerRef ownerPlayerRef)
        {
            // 既に誰か乗っていたら乗れないよん
            if (!Runner.IsServer || OwnerPlayerRef != PlayerRef.None) return;

            ForceSetInteractable = false;

            // set input authority
            OwnerPlayerRef = ownerPlayerRef;
            LastUsedCooldownTime = -9999f;
            Object.AssignInputAuthority(ownerPlayerRef);
            // playerの状態切り替え
            _ownerPlayerManager = StaticServiceLocator.Instance.Get<InGameManager>().PlayerDataDic[ownerPlayerRef]
                .GetComponent<PlayerManager>();
            _ownerPlayerManager.SetControlState(PlayerManager.PlayerControlState.ForcedControl);
            _ownerPlayerManager.RPC_SetUseGrav(false);
            _ownerPlayerManager.RPC_SetColliderActive(false);
            _ownerPlayerManager.RPC_SetMeshActive(false);
            RPC_ChangeDescriptionUI(ownerPlayerRef, ControlDescriptionType.AirPlane);
            RPC_SetIsKinematic(false);
            // 乗った時刻を記録
            GetOnTime = Runner.SimulationTime;
            //隊列があった場合の処理
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

        void GetOff()
        {
            if (!Runner.IsServer || OwnerPlayerRef == PlayerRef.None) return;

            ForceSetInteractable = true;
            
            PlayerDatabase.Instance.PlayerDataDic.TryGet(OwnerPlayerRef, out var playerData);
            RPC_ChangeDescriptionUI(OwnerPlayerRef, playerData.CharacterType == CharacterType.Sarutobi? ControlDescriptionType.Sarutobi : ControlDescriptionType.Player);
            var chara = PlayerDatabase.Instance.PlayerDataDic[OwnerPlayerRef].CharacterType;

            // Authority
            OwnerPlayerRef = PlayerRef.None;
            Object.RemoveInputAuthority();
            // playerの状態切り替えよん
            _ownerPlayerManager.SetControlState(PlayerManager.PlayerControlState.Normal);
            _ownerPlayerManager.RPC_SetUseGrav(true);
            _ownerPlayerManager.RPC_SetColliderActive(true);
            _ownerPlayerManager.RPC_SetMeshActive(true);
            // 降りる場所にセット
            _ownerPlayerManager.transform.position = _getOffPoint.position;

            // 飛行機を初期位置・回転に戻す（ティラノと同じ仕組み）
            transform.SetPositionAndRotation(_initialPosition, _initialRotation);

            // クールダウン開始
            var time = CooldownTimeDictionary.Dictionary.TryGetValue(CharacterType.All, out var all)
                ? all
                : CooldownTimeDictionary.Dictionary.GetValueOrDefault(chara, 0f);
            SetCooldown(time);

            // パラメータリセット
            CurrentAccel = 0;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            RPC_SetIsKinematic(true);
            _interactTimer = 0f;

            //隊列がある場合の処理
            if (_ownerPlayerManager.TryGetComponent<FormationManager>(out var formationManager))
            {
                formationManager.WarpFriendNearPlayerWhenGrounded(
                    _ownerPlayerManager.GetComponent<PlayerMovement>());
            }
            AudioBroadcaster.RequestStopSound(SoundCues.SE.ZeroFighter_Interact.Name);          // 飛行中のループ音(サウンドデータの関係でInteractの音で判定)
            AudioBroadcaster.RequestStopSound(SoundCues.SE.ZeroFighter_TakeoffGunFire.Name);    // もしくは射撃音を止める
        }

        void OnChangeOwnerPlayerRef()
        {
            if (OwnerPlayerRef == Runner.LocalPlayer)
            {
                _cameraController.SetCameraPriority(15);
            }
            else
            {
                _cameraController.SetCameraPriority(5);
            }
        }

        private void LateUpdate()
        {
            if (_isSpawned == false) return;
            // camera 操作
            if (HasInputAuthority)
            {
                _cameraController.InputToCamera(GameInput.I.Player.Look.ReadValue<Vector2>(), Time.deltaTime);
            }

            // rotate prop
            Vector3 euler = _prop.eulerAngles;
            euler.z += CurrentAccel * _propSpeedRate * Time.deltaTime;
            euler.z = euler.z % 360f < 0 ? euler.z % 360f + 360f : euler.z % 360f;
            _prop.eulerAngles = euler;

            DisplayDebug();
        }

        void DisplayDebug()
        {
            if (_velocityText) _velocityText.text = "velocity : " + _rb.linearVelocity.ToString();
            if (_forwardSpeedText)
            {
                float fs = Vector3.Dot(_rb.linearVelocity, transform.forward);
                _forwardSpeedText.text = "forward speed : " + fs.ToString("F2");
            }

            if (_currentAccelText) _currentAccelText.text = "current accel : " + CurrentAccel.ToString("F2");
            if (_angleText) _angleText.text = "angle : " + transform.eulerAngles.ToString("F2");
            if (_isUpText) _isUpText.text = "is up : " + (Vector3.Angle(transform.up, Vector3.up) <= 90);
        }

        protected override void OnInteract(IInteractableContext context)
        {
            if (OwnerPlayerRef == PlayerRef.None) GetOn(PlayerRef.FromEncoded(context.Interactor));
            else if (OwnerPlayerRef == PlayerRef.FromEncoded(context.Interactor)) GetOff();
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_SetIsKinematic(bool kinematic)
        {
            _rb.isKinematic = kinematic;
        }

        private void OnCollisionStay(Collision other)
        {
            if (_rb.linearVelocity.y > 0) return;

            foreach (ContactPoint contact in other.contacts)
            {
                if (Vector3.Angle(contact.normal, Vector3.up) <= 45)
                {
                    _onGround = true;
                    _groundNormal = contact.normal;

                    if (contact.thisCollider.gameObject == _wheelObj)
                    {
                        _onGroundWheel = true;
                        break;
                    }
                }
            }
        }
    }
}
