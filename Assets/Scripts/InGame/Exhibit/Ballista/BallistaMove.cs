using Fusion;
using InGame.Player;
using September.Common;
using Unity.Mathematics;
using UnityEngine;

namespace September.InGame.Exhibit
{
	public class BallistaMove : NetworkBehaviour, IProjectileMovement
	{ 
		[SerializeField] private Transform _barrel;
		[SerializeField] private Transform _rotateBase;
		[SerializeField] private Transform _shootPos;
		[SerializeField] private Transform _playerPos;
		[SerializeField] private CameraController _cameraController;
		[SerializeField] private LayerMask _layerMask;
		[SerializeField] private float _playerOffset = 3;
		[SerializeField] private float _rotateSpeed = 1.5f;

		[Header("CameraAngleLimit")] [SerializeField]
		private bool _useYawLimit;
		[SerializeField] private Vector2 _pitchLimit = new(-90f, 90f);
		[SerializeField] private Vector2 _yawLimit = new(-90f, 90f);
		[SerializeField] private Vector3 _baseUp;
		[SerializeField] private Vector3 _barrelRight;
		private float _baseYaw;
		private float _basePitch;
		
		[Networked] private NetworkObject PlayerObject { get; set; }
		[Networked] private float Pitch { get; set; }
		[Networked] private float Yaw { get; set; }

		public override void Spawned()
		{
			_cameraController.Init(true);
			_baseYaw = _barrel.rotation.eulerAngles.y;
			Yaw = _baseYaw;
			_basePitch = _barrel.rotation.eulerAngles.x;
			Pitch = _basePitch;
		}

		public void Render()
		{
			ModelRotate();
			UpdatePlayerPosition();
			if (HasInputAuthority)
			{
				RotateCamera(GameInput.I.Player.Look.ReadValue<Vector2>(), Time.deltaTime);
			}
		}

		public void InitializeStateAuthority(NetworkObject playerObject, PlayerRef playerRef)
		{
			PlayerObject = playerObject;
		}

		public void Initialize()
		{
			if(HasInputAuthority)
			{
				_cameraController.SetCameraRotate(_basePitch, _baseYaw);
			}
		}

		private void ModelRotate()
		{
			// 軸をYawに置き換える
			var _baseAngle = _rotateBase.eulerAngles;
			_baseAngle.y = Yaw;
			_rotateBase.rotation = Quaternion.Euler(_baseAngle);
			
			var currentBarrelAngles = _barrel.eulerAngles;
			currentBarrelAngles.x = Pitch;
			_barrel.rotation = Quaternion.Euler(currentBarrelAngles);
		}

		void IProjectileMovement.Update(PlayerInput input)
		{
			var cameraForward = input.DesiredLookDirection;
			Debug.DrawRay(_cameraController.GetCameraPosition(), cameraForward * 100, Color.green);
			Debug.DrawRay(_barrel.position, _barrel.forward * 100, Color.red);
			
			if (Physics.Raycast(input.CameraPosition, cameraForward, out var hit, 100, _layerMask))
			{
				var baseDir = (hit.point - _rotateBase.position).normalized;
				var lookRotation = Quaternion.LookRotation(baseDir);
				Yaw = lookRotation.eulerAngles.y;
				
				var barrelDir = (hit.point - _barrel.position).normalized;
				var barrelRotation = Quaternion.LookRotation(barrelDir);
				Pitch = barrelRotation.eulerAngles.x;
			}
			else
			{
				var lookRotation = Quaternion.LookRotation(cameraForward);
				Yaw = lookRotation.eulerAngles.y;
				Pitch = lookRotation.eulerAngles.x;
			}

			UpdatePlayerPosition();
		}

		private void RotateCamera(Vector2 input, float deltaTime)
		{
			_cameraController.RotateCamera(input, deltaTime);

			// 角度を-180~180に変換してclampする
			var pitch = Mathf.DeltaAngle(0, _cameraController.CameraPitch);
			pitch = Mathf.Clamp(pitch, _pitchLimit.x, _pitchLimit.y);
			
			float yaw = _cameraController.CameraYaw;
			if (_useYawLimit)
			{
				yaw = Mathf.DeltaAngle(_baseYaw, _cameraController.CameraYaw);
				yaw = Mathf.Clamp(yaw, _yawLimit.x, _yawLimit.y) + _baseYaw;
			}
			
			_cameraController.SetCameraRotate(pitch, yaw);
		}

		private void UpdatePlayerPosition()
		{
			if(!PlayerObject) return;
			PlayerObject.transform.position = _playerPos.position;
			PlayerObject.transform.rotation = _playerPos.rotation;
		}

		public void Reset()
		{
			Yaw = _baseYaw;
			Pitch = _basePitch;
			PlayerObject = null;
		}
	}
}