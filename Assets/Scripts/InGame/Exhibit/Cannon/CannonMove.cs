using Fusion;
using InGame.Player;
using September.Common;
using UnityEngine;

namespace September.InGame.Exhibit
{
	public class CannonMove : NetworkBehaviour, IProjectileMovement
	{
		[Header("移動関連")] [SerializeField] private Transform _cannonBase;
		[SerializeField] private CameraController _cameraController;
		[SerializeField] private Transform _cannonBarrel;
		[SerializeField] private Transform _waitPlayerPos;
		[SerializeField] private float _baseRotateSpeed;
		[SerializeField] private float _barrelRotateSpeed;

		[Header("射撃角度制限")] [SerializeField] private bool _useYawAngleLimit = true;
		// minをxとして、maxをyとして扱う
		[SerializeField] private Vector2 _baseRotateAngleLimit = new(-90, 90);
		[SerializeField] private Vector2 _barrelRotateAngleLimit = new(-90, 90);
		private Quaternion _baseDefaultRotation;
		private Quaternion _barrelDefaultRotation;
		[Networked] private NetworkObject Player { get; set; }
		[Networked] private Quaternion BaseRotation { get; set; }
		[Networked] private Quaternion BarrelRotation { get; set; }

		public override void Spawned()
		{
			base.Spawned();
			_cameraController.Init(true);
		}

		public void Render()
		{
			_cannonBase.localRotation = BaseRotation;
			_cannonBarrel.localRotation = BarrelRotation;
			UpdatePlayerPos();
			CameraRotate(_cannonBase.rotation);
		}

		public void InitializeStateAuthority(NetworkObject playerObject, PlayerRef playerRef)
		{
			Player = playerObject;
		}

		public void Initialize()
		{
			
		}

		void IProjectileMovement.Update(PlayerInput input)
		{
			if (!HasStateAuthority) return;
			CannonRotate(input.MoveDirection.x, input.LookDirection.y);
			UpdatePlayerPos();
		}

		public void Reset()
		{
			Player = null;
			RPC_Refresh();
		}

		[Rpc(RpcSources.All, RpcTargets.All)]
		public void RPC_Refresh()
		{
			BarrelRotation = _barrelDefaultRotation;
			BaseRotation = _baseDefaultRotation;
		}

		private void UpdatePlayerPos()
		{
			if(!Player) return;
			Player.transform.rotation = _waitPlayerPos.rotation;
			Player.transform.position = _waitPlayerPos.position;
		}

		private void CannonRotate(float baseRotateInput, float barrelRotateInput)
		{
			baseRotateInput = Mathf.Clamp(baseRotateInput, -1, 1);
			barrelRotateInput = Mathf.Clamp(barrelRotateInput, -1, 1);

			// 土台の回転
			var currentBaseAxis = _cannonBase.localEulerAngles.y +
			                      _baseRotateSpeed * baseRotateInput;
			currentBaseAxis = WrapAngle(currentBaseAxis);
			BaseRotation = Quaternion.Euler(0,
				_useYawAngleLimit
					? Mathf.Clamp(currentBaseAxis, _baseRotateAngleLimit.x, _baseRotateAngleLimit.y)
					: currentBaseAxis, 0);

			// 砲身の回転
			var currentBarrelAxis = _cannonBarrel.localEulerAngles.x +
			                        _barrelRotateSpeed * barrelRotateInput;
			currentBarrelAxis = WrapAngle(currentBarrelAxis);
			BarrelRotation = Quaternion.Euler(
				Mathf.Clamp(currentBarrelAxis, _barrelRotateAngleLimit.x, _barrelRotateAngleLimit.y), 0, 0);
		}

		private void CameraRotate(Quaternion rotation)
		{
			_cameraController.SmoothRotateCameraTo(rotation);
		}
		
		/// <summary>
		///     角度を-180~180に変換する
		/// </summary>
		private float WrapAngle(float angle)
		{
			angle %= 360;
			if (angle > 180) angle -= 360;
			return angle;
		}
	}
}