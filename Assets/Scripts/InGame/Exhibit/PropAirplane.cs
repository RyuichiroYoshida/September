using Fusion;
using InGame.Interact;
using InGame.Player;
using September.Common;
using September.InGame.Common;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;
using PlayerInput = September.Common.PlayerInput;

namespace InGame.Exhibit
{
    public class PropAirplane : InteractableBase
    {
        [Header("Ride")]
        [SerializeField] private Transform _getOffPoint;
        [Header("Move")]
        [SerializeField] private float _grav;
        [SerializeField] private Vector3 _drag;
        [SerializeField] private float _jerk;
        [SerializeField] private float _propDrag;
        [SerializeField] private float _maxAccel;
        [SerializeField] private float _lift;
        [SerializeField, Tooltip("重力を打ち消す速度")] private float _forwardSpeedBalancedByGravity;
        [SerializeField] private GameObject _wheelObj;
        [Header("Rotate")]
        [SerializeField] private float _angularDrag;
        [SerializeField] private float _rotSpeedPitch;
        [SerializeField] private float _rotSpeedRoll;
        [SerializeField] private float _rotSpeedYaw;
        [SerializeField] private float _rotReturnSpeedRoll;
        [Space]
        [SerializeField] private float _angularGroundDrag;
        [SerializeField] private float _rotSpeedGroundYaw;
        [Header("Prop")]
        [SerializeField] private Transform _prop;
        [SerializeField] private float _propSpeedRate;
        [Header("Debug")]
        [SerializeField] private TMP_Text _velocityText;
        [SerializeField] private TMP_Text _forwardSpeedText;
        [SerializeField] private TMP_Text _currentAccelText;
        [SerializeField] private TMP_Text _angleText;
        [SerializeField] private TMP_Text _isUpText;

        private Rigidbody _rb;
        private AirplaneCamera _cameraController;
        private PlayerRef _ownerPlayerRef;
        private PlayerManager _ownerPlayerManager;
        // move
        private bool _onGround;
        private bool _onGroundWheel;
        private Vector3 _groundNormal;
        private bool IsGround => _onGroundWheel;
        // FixedUpdateNetwork で AddForce するときの補正
        private float PhysicsCoefficient => Runner.DeltaTime / Time.fixedDeltaTime;
        
        [Networked] private float CurrentAccel { get; set; }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _cameraController = GetComponent<AirplaneCamera>();
        }

        public override void FixedUpdateNetwork()
        {
            if (HasStateAuthority)
            {
                if (GetInput<PlayerInput>(out var input))
                {
                    if (input.Buttons.IsSet(PlayerButtons.Dash))
                    {
                        if (IsGround) AddSpeedBack();
                        else PropSlowDown();
                    }
                    else AddSpeedForward();
                    RotatePlane(input.MoveDirection);
                    // playerオブジェクトのpositionを固定する
                    _ownerPlayerManager.transform.position = transform.position;
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
            _rb.AddForce(_grav * Mathf.Clamp01(forwardSpeed / _forwardSpeedBalancedByGravity) * PhysicsCoefficient * Vector3.up, ForceMode.Acceleration);
        }

        /// <summary> 外部的なAddForce </summary>
        void ApplyExternalForces()
        {
            // grav
            _rb.AddForce(_grav * PhysicsCoefficient * Vector3.down, ForceMode.Acceleration);
            // drag
            if (_rb.linearVelocity.sqrMagnitude > 0.0001f)
            {
                Vector3 dragLocalVelocity = Vector3.Scale(transform.InverseTransformDirection(_rb.linearVelocity), _drag);
                Vector3 dragWorldVelocity = transform.TransformDirection(dragLocalVelocity);
                _rb.AddForce(_rb.linearVelocity.magnitude * PhysicsCoefficient * -dragWorldVelocity, ForceMode.Acceleration);
            }
            // angular drag
            if (_rb.angularVelocity.sqrMagnitude > 0.0001f) 
                _rb.AddTorque((IsGround ? _angularGroundDrag : _angularDrag) * _rb.angularVelocity.magnitude * PhysicsCoefficient * -_rb.angularVelocity, ForceMode.Acceleration);
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
                    torque.z = moveDir.x * _rotSpeedRoll * forwardSpeed * (isUp ? -1 : 1) * Mathf.Abs(Mathf.DeltaAngle(eulerZ, 80) / 90);
                }
                else if (moveDir.x > 0 && Mathf.Abs(280 - eulerZ) > 10)
                {
                    torque.z = moveDir.x * _rotSpeedRoll * forwardSpeed * (isUp ? -1 : 1) * Mathf.Abs(Mathf.DeltaAngle(eulerZ, 280) / 90);
                }
                
                torque = transform.TransformDirection(torque);
                // yaw は world 回転
                torque.y += moveDir.x * _rotSpeedYaw * forwardSpeed;
                //torque += moveDir.x * _rotSpeedYaw * forwardSpeed * (Quaternion.Euler(transform.eulerAngles.x, 0, 0) * Vector3.up);
                _rb.AddTorque(torque * PhysicsCoefficient, ForceMode.Acceleration);
            }
        }

        void GetOn(PlayerRef ownerPlayerRef)
        {
            // 既に誰か乗っていたら乗れないよん
            if (!Runner.IsServer || _ownerPlayerRef != PlayerRef.None) return;
            
            // set input authority 
            _ownerPlayerRef = ownerPlayerRef;
            Object.AssignInputAuthority(_ownerPlayerRef);
            // camera の切り替え
            _cameraController.SetCameraPriority(15);
            // playerの状態切り替え
            _ownerPlayerManager = StaticServiceLocator.Instance.Get<InGameManager>().PlayerDataDic[_ownerPlayerRef].GetComponent<PlayerManager>();
            _ownerPlayerManager.SetControlState(PlayerManager.PlayerControlState.ForcedControl);
            _ownerPlayerManager.RPC_SetColliderActive(false);
            _ownerPlayerManager.RPC_SetMeshActive(false);
        }

        void GetOff()
        {
            if (!Runner.IsServer || _ownerPlayerRef == PlayerRef.None) return;
            
            // set input authority 
            _ownerPlayerRef = PlayerRef.None;
            Object.RemoveInputAuthority();
            // camera の切り替え
            _cameraController.SetCameraPriority(5);
            // playerの状態切り替えよ💛
            _ownerPlayerManager.SetControlState(PlayerManager.PlayerControlState.Normal);
            _ownerPlayerManager.RPC_SetColliderActive(true);
            _ownerPlayerManager.RPC_SetMeshActive(true);
            // 降りる場所にセット
            _ownerPlayerManager.transform.position = _getOffPoint.position;
        }

        private void LateUpdate()
        {
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
            
            // debug
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

        protected override bool OnValidateInteraction(IInteractableContext context, CharacterType charaType)
        {
            return _ownerPlayerRef == PlayerRef.None || _ownerPlayerRef == PlayerRef.FromEncoded(context.Interactor);
        }

        protected override void OnInteract(IInteractableContext context)
        {
            if (_ownerPlayerRef == PlayerRef.None) GetOn(PlayerRef.FromEncoded(context.Interactor));
            else if (_ownerPlayerRef == PlayerRef.FromEncoded(context.Interactor)) GetOff();
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
