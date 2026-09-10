using System;
using Fusion;
using UnityEngine;

namespace September.InGame.Exhibit
{
	public class Projectile : NetworkBehaviour
	{
		private LayerMask _hitLayer;
		private GameObject _projectile;
		private TickTimer _lifeTimer;
		
		/// <summary>
		/// サーバーで実行されるHit時のコールバック処理
		/// </summary>
		private Action<Vector3, Quaternion, GameObject> OnHitCallback;

		[Networked] private ProjectileLauncher.ProjectileData CurrentProjectileData { get; set; }
		[Networked] private PlayerRef PlayerRef { get; set; }
		

		public override void FixedUpdateNetwork()
		{
			base.FixedUpdateNetwork();
			if(HasStateAuthority)
			{
				CurrentProjectileData = ProjectileUpdate(CurrentProjectileData);
				if(_lifeTimer.Expired(Runner))
				{
					Runner.Despawn(Object);
					return;
				}
			}
			
			if (CurrentProjectileData.HasHit)
			{
				OnHitCallback = null;
				Runner.Despawn(Object);
			}
		}

		public override void Render()
		{
			base.Render();
			RenderProjectile();
		}

		public override void Despawned(NetworkRunner runner, bool hasState)
		{
			OnHitCallback = null;
			Destroy(_projectile.gameObject);
		}

		/// <summary>
		/// 全てのクライアント共通の値の初期化処理
		/// </summary>
		/// <param name="projectilePrefab"></param>
		/// <param name="hitLayer"></param>
		public void Initialized(GameObject projectilePrefab, LayerMask hitLayer)
		{
			_projectile = Instantiate(projectilePrefab);
			_hitLayer = hitLayer;
		}

		public void Fire(ProjectileLauncher.ProjectileData projectileData, PlayerRef playerRef,
			Action<Vector3, Quaternion, GameObject> onHitCallback)
		{
			CurrentProjectileData = projectileData;
			PlayerRef = playerRef;
			OnHitCallback = onHitCallback;
			_lifeTimer = TickTimer.CreateFromSeconds(Runner, CurrentProjectileData.LifeTime);
		}
		
		private void RenderProjectile()
		{
			if(!_projectile) return;
			if (CurrentProjectileData.HasHit) return;
			
			// particleの更新
			_projectile.transform.position = CurrentProjectileData.CurrentPosition;
			_projectile.transform.forward = CurrentProjectileData.CurrentForward.magnitude > 0
				? CurrentProjectileData.CurrentForward
				: Vector3.forward;
		}

		private ProjectileLauncher.ProjectileData ProjectileUpdate(ProjectileLauncher.ProjectileData data)
		{
			if (data.HasHit) return data;

			var currentPos = CalculateTrajectoryPosition(data.StartPosition,
				data.InitialVelocity, data.Gravity, data.Timer);
			var nextPos = CalculateTrajectoryPosition(data.StartPosition,
				data.InitialVelocity, data.Gravity, data.Timer + Runner.DeltaTime);

			// 弾の着弾確認
			var movement = nextPos - currentPos;
			if (Physics.Raycast(currentPos, movement.normalized, out var hit, movement.magnitude, _hitLayer))
			{
				data.HasHit = true;
				OnHitCallback?.Invoke(hit.point, Quaternion.LookRotation(hit.normal), hit.transform.gameObject);
				return data;
			}

			// particleの更新
			data.CurrentPosition = currentPos;
			data.CurrentForward = movement.normalized;
			data.Timer += Runner.DeltaTime;

			return data;
		}
		
		/// <summary>
		///     特定の時間での放物線位置を計算する
		/// </summary>
		/// <returns>入力時間の時の位置</returns>
		public static Vector3 CalculateTrajectoryPosition(Vector3 startPos, Vector3 velocity, Vector3 gravity,
			float time)
		{
			return startPos + velocity * time + 0.5f * gravity * (time * time);
		}
	}
}