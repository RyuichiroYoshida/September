using Fusion;
using Fusion.Addons.Physics;
using InGame.Player;
using UnityEngine;
using September.Common;

namespace September.InGame.Exhibit
{
	[DefaultExecutionOrder(-10)]
	public class GliderMove : NetworkBehaviour, IProjectileMovement
	{
		[SerializeField] private Rigidbody _rb;
		[SerializeField] private NetworkRigidbody3D _networkRigidbody;
		[SerializeField] private Transform _startPos;
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


		private CameraController _cameraController;
		private float _startTime;
		[Networked] public bool IsFinished { get; private set; }
		[Networked] private PlayerManager Player { get; set; }
		[Networked] private Vector3 Velocity { get; set; }

		public override void Spawned()
		{
			base.Spawned();
			_cameraController = GetComponent<CameraController>();
			_cameraController.Init(true);
			Reset();
			Debug.Log($"Spawned: {_rb.position} / {_rb.rotation} startPos: {_startPos.position} / {_startPos.rotation}");
		}

		void IProjectileMovement.Render()
		{
			base.Render();
			PlayTiltAnimation(Velocity);

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
			_startTime = Runner.Tick;
			IsFinished = false;

			Player = playerObject.GetComponent<PlayerManager>();
			if (Player.TryGetComponent(out PlayerMovement playerMovement))
			{
				playerMovement.UseGravity = false;
			}
		}

		private void GliderInit()
		{
			_rb.position = _startPos.position;
			_rb.rotation = _startPos.rotation;
			Debug.Log($"GliderInit: {_rb.position} / {_rb.rotation}");
			_networkRigidbody.Teleport(_rb.position, _rb.rotation);
			
			_rb.linearVelocity = Vector3.zero;
			_rb.angularVelocity = Vector3.zero;
			_rb.useGravity = false;
		}

		void IProjectileMovement.Update(PlayerInput input)
		{
			if (IsFinished) return;
			if (IsLanded())
			{
				Debug.Log($"startTime:{_startTime} Tick:{Runner.Tick}");
				IsFinished = true;
				Debug.Log("着地しました");
				return;
			}

			var velocity = SetVelocity(_rb.linearVelocity, input.MoveDirection, input.CameraYaw);
			_rb.linearVelocity = velocity;
			SetPlayerPos();
			RPC_SetActive(true);

			_gripObject.position = _controlObject.position;
			_gripObject.rotation = _controlObject.rotation;

			if (HasStateAuthority)
			{
				Velocity = velocity;
			}
			// Debug.Log(
			// 	$"RB: {_rb.position} / " +
			// 	$"Transform: {transform.position}"
			// );
		}

		public void Reset()
		{
			Debug.Log($"Reset: {_rb.position} / {_rb.rotation}");
			_rb.linearVelocity = Vector3.zero;
			_rb.angularVelocity = Vector3.zero;

			_rb.position = _startPos.position;
			_rb.rotation = _startPos.rotation;

			IsFinished = true;
			
			if(!Player) return;
			
			if(Player.TryGetComponent(out Rigidbody playerRb))
			{
				playerRb.linearVelocity = Vector3.zero;
				playerRb.angularVelocity = Vector3.zero;
			}

			if (Player.TryGetComponent(out PlayerMovement playerMovement))
			{
				playerMovement.UseGravity = true;
			}
			
			Player.transform.rotation = Quaternion.identity;
			Player = null;
			RPC_SetActive(false);
		}

		private Vector3 SetVelocity(Vector3 velocity, Vector2 input, float cameraYaw)
		{
			var yawRotation = Quaternion.Euler(0f, cameraYaw, 0f);

			var cameraForward = yawRotation * Vector3.forward;
			var cameraRight = yawRotation * Vector3.right;
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

		private void PlayTiltAnimation(Vector3 velocity)
		{
			// 移動方向に回転を合わせる
			velocity.y = 0;
			if (velocity.sqrMagnitude < 0.001f)
				return;
			var targetY =
				Mathf.Atan2(velocity.x, velocity.z) * Mathf.Rad2Deg;
			var currentY = _controlObject.localEulerAngles.y;
			var nextY = Mathf.MoveTowardsAngle(
				currentY,
				targetY,
				50 * Time.fixedDeltaTime
			);

			var yawRotate = Quaternion.Euler(0, nextY, 0);
			// 移動方向に傾ける
			var localVelocity =
				Quaternion.Inverse(yawRotate) * velocity;
			var normalizedVelocity =
				Vector3.ClampMagnitude(localVelocity / _maxSpeed, 1f);
			var tiltAngle = normalizedVelocity * _playerTiltAngle;

			Debug.Log($"Render Animation {velocity} {tiltAngle} angleLimit:{_playerTiltAngle} normalized:{normalizedVelocity}");
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