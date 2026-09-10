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
		[SerializeField] private Transform _rotateObject;
		[SerializeField] private Transform _viewObject;
		[SerializeField] private Transform _playerPos;
		[SerializeField] private float _maxSpeed = 5f;
		[SerializeField] private float _acceleration = 10f;
		[SerializeField] private float _gravity = -9.81f;
		[Header("接地判定")]
		[SerializeField] private float _LandingHeight = 1f;
		[SerializeField] private float _raycastRadius = 0.5f;
		[SerializeField] private LayerMask _groundLayer;

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
			RPC_PlayerRide(Player, true);
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

			if (HasStateAuthority)
			{
				var dir = Quaternion.LookRotation(input.DesiredLookDirection) * input.MoveDirection;
				MoveDirection = new Vector2(dir.x, dir.z);
				Velocity = velocity;
			}
		}

		public void Reset()
		{
			IsFinished = true;
			RPC_SetActive(false);

			if (!Player) return;

			if (Player.TryGetComponent(out Rigidbody playerRb))
			{
				playerRb.linearVelocity = Vector3.zero;
				playerRb.angularVelocity = Vector3.zero;
			}
			
			RPC_PlayerRide(Player, false);
			
			// _rb初期化前にplayerの位置をGliderに合わせる
			Player.transform.position = _rb.position;
			Player.transform.rotation = Quaternion.identity;
			Player = null;
			
			GliderInit();
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

		private void PlayTiltAnimation(Vector3 velocity, Vector2 moveDirection)
		{
			var moveDri = new Vector3(moveDirection.x, 0f, moveDirection.y).normalized;
			velocity.y = 0;

			// Yawの更新 回転方向の基準は入力を優先。
			// 入力が無い場合は、velocityの向きを基準に回転
			var rotationSource = moveDri.sqrMagnitude >= 0.001f ? moveDri : velocity;
			var currentY = _rotateObject.localEulerAngles.y;
			var nextY = currentY;
			if (rotationSource.sqrMagnitude >= 0.001f)
			{
				var targetY =
					Mathf.Atan2(rotationSource.x, rotationSource.z) * Mathf.Rad2Deg;

				nextY = Mathf.MoveTowardsAngle(
					currentY,
					targetY,
					_rotateSpeed * Time.fixedDeltaTime
				);
			}

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

			_rotateObject.rotation = yawRotate * Quaternion.Euler(tiltAngle.z, 0, tiltAngle.x);
		}

		private bool IsLanded()
		{
			var ray = new Ray(_rb.position, Vector3.down);
			if (Physics.SphereCast(ray, _raycastRadius, out _, _LandingHeight, _groundLayer)) return true;
			return false;
		}

		[Rpc]
		private void RPC_SetActive(bool isActive)
		{
			_viewObject.gameObject.SetActive(isActive);
		}

		[Rpc]
		private void RPC_PlayerRide(PlayerManager player, bool isRide)
		{
			if (isRide)
			{
				player.BeginRideView(_playerPos, Vector3.zero);
			}
			else
			{
				player.EndRideView();
			}
		}
	}
}