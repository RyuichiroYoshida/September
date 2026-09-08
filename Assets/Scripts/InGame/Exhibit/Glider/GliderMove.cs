using Fusion;
using Fusion.Addons.Physics;
using InGame.Player;
using UnityEngine;
using September.Common;

namespace September.InGame.Exhibit
{
	public class GliderMove : NetworkBehaviour, IProjectileMovement
	{
		[SerializeField] private Rigidbody _rb;
		[SerializeField] private NetworkRigidbody3D _networkRigidbody;
		[SerializeField] private Transform _controlObject;
		[SerializeField] private Transform _gripObject;
		[SerializeField] private Transform _camera;
		[SerializeField] private Transform _playerPos;
		[SerializeField] private Transform _cameraPos;
		[SerializeField] private LayerMask _groundLayer;
		[SerializeField] private float _maxSpeed = 5f;
		[SerializeField] private float _acceleration = 10f;
		[SerializeField] private float _gravity = -9.81f;
		[SerializeField] private float _LandingHeight = 1f;

		[Header("傾きアニメーション設定")] [SerializeField]
		private float _playerTiltAngle = 45f;

		[SerializeField] private float _rotateSpeed = 50f;

		private Vector3 _startPos;
		private CameraController _cameraController;
		[Networked,HideInInspector] public bool IsFinished { get; private set; }
		[Networked] private PlayerManager Player { get; set; }
		[Networked] private Vector3 Velocity { get; set; }
		[Networked] private Vector2 MoveDirection { get; set; }

		public override void Spawned()
		{
			base.Spawned();
			_cameraController = GetComponent<CameraController>();
			_cameraController.Init(true);
			_startPos = _rb.position;
			if (HasStateAuthority)
				GliderInit();
		}

		void IProjectileMovement.Render()
		{
			base.Render();
			PlayTiltAnimation(Velocity, MoveDirection);

			if (HasInputAuthority)
			{
				SetCameraPos();
				_cameraController.RotateCamera(GameInput.I.Player.Look.ReadValue<Vector2>(), Time.fixedDeltaTime);
			}
		}

		public void Initialize()
		{
		}

		public void InitializeStateAuthority(NetworkObject playerObject, PlayerRef playerRef)
		{
			GliderInit();
			IsFinished = false;

			Player = playerObject.GetComponent<PlayerManager>();
			if (Player.TryGetComponent(out PlayerMovement playerMovement)) playerMovement.UseGravity = false;
			RPC_SetActive(true);
		}

		private void GliderInit()
		{
			_rb.position = _startPos;
			_rb.rotation = Quaternion.identity;

			_rb.linearVelocity = Vector3.zero;
			_rb.angularVelocity = Vector3.zero;
		}

		void IProjectileMovement.Update(PlayerInput input)
		{
			if (IsFinished) return;
			if (IsLanded())
			{
				IsFinished = true;
				return;
			}
			var velocity = SetVelocity(_rb.linearVelocity, input.MoveDirection,
				input.DesiredLookDirection);
			_rb.linearVelocity = velocity;
			SetPlayerPos();

			_gripObject.position = _controlObject.position;
			_gripObject.rotation = _controlObject.rotation;

			if (HasStateAuthority)
			{
				var dir = Quaternion.LookRotation(input.DesiredLookDirection) * input.MoveDirection;
				MoveDirection = new Vector2(dir.x, dir.z);
				Velocity = velocity;
			}
		}

		public void Reset()
		{
			GliderInit();

			IsFinished = true;

			if (!Player) return;

			if (Player.TryGetComponent(out Rigidbody playerRb))
			{
				playerRb.linearVelocity = Vector3.zero;
				playerRb.angularVelocity = Vector3.zero;
			}

			if (Player.TryGetComponent(out PlayerMovement playerMovement)) playerMovement.UseGravity = true;

			Player.transform.rotation = Quaternion.identity;
			Player = null;
			RPC_SetActive(false);
		}

		private Vector3 SetVelocity(Vector3 velocity, Vector2 input, Vector3 cameraForward)
		{   
			cameraForward.y = 0f;
			
			// cameraForwardを+90度回転させたものがcameraRight
			var cameraRight = new Vector3(cameraForward.z, 0f, -cameraForward.x);

			var inputDirection = cameraForward * input.y + cameraRight * input.x;
			var targetVelocity = inputDirection.normalized * _maxSpeed;
			velocity = Vector3.MoveTowards(velocity, targetVelocity, _acceleration * Time.fixedDeltaTime);
			velocity.y = _gravity;
			return velocity;
		}

		private void SetPlayerPos()
		{
			if (!Player) return;
			Player.transform.position = _playerPos.position;
			Player.transform.rotation = _playerPos.rotation;
		}

		private void SetCameraPos()
		{
			_camera.position = _cameraPos.position;
			_camera.rotation = _cameraPos.rotation;
		}

		private void PlayTiltAnimation(Vector3 velocity, Vector2 moveDirection)
		{
			var moveDri = new Vector3(moveDirection.x, 0f, moveDirection.y).normalized;
			velocity.y = 0;

			// 回転方向の基準はmoveDirection(入力)を優先。
			// 入力が無い(≒moveDirectionがほぼ0)場合は、velocityの向きにフォールバック
			var rotationSource = moveDri.sqrMagnitude >= 0.001f ? moveDri : velocity;
			if (rotationSource.sqrMagnitude < 0.001f)
				return;

			var targetY =
				Mathf.Atan2(rotationSource.x, rotationSource.z) * Mathf.Rad2Deg;
			var currentY = _controlObject.localEulerAngles.y;
			var nextY = Mathf.MoveTowardsAngle(
				currentY,
				targetY,
				_rotateSpeed * Time.fixedDeltaTime
			);

			var yawRotate = Quaternion.Euler(0, nextY, 0);
			// 現在の向きと移動方向のズレ
			// 90度以降は0
			var facingFactor = Mathf.Clamp01(Vector3.Dot(yawRotate * Vector3.forward, velocity.normalized));

			// 移動方向に傾ける(ズレているほど傾きを抑える)
			var localVelocity =
				Quaternion.Inverse(yawRotate) * velocity;
			var normalizedVelocity =
				Vector3.ClampMagnitude(localVelocity / _maxSpeed, 1f);
			var tiltAngle = normalizedVelocity * (_playerTiltAngle * facingFactor);

			_controlObject.rotation = yawRotate * Quaternion.Euler(tiltAngle.z, 0, tiltAngle.x);
		}

		private bool IsLanded()
		{
			var ray = new Ray(_rb.position, Vector3.down);
			if (Physics.Raycast(ray, out _, _LandingHeight, _groundLayer)) return true;
			return false;
		}

		[Rpc]
		private void RPC_SetActive(bool isActive)
		{
			_gripObject.gameObject.SetActive(isActive);
		}
	}
}