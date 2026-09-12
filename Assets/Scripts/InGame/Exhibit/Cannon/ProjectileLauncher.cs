using System;
using Fusion;
using September.Common;
using September.InGame.Effect;
using UnityEngine;

namespace September.InGame.Exhibit
{
	public class ProjectileLauncher : NetworkBehaviour
	{
		[SerializeField] private Transform _projectileSpawnPoint;
		[SerializeField] private Projectile _projectilePrefab;
		[SerializeField] private NetworkObject _projectileEffectPrefab;
		[SerializeField] private EffectType _shootEffectType;
		[SerializeField] private float _simulationStepTime = 0.1f;
		[SerializeField] private float _lifeTime = 10f;
		[SerializeField] private Vector3 _gravity = new(0, -9.81f, 0);
		[SerializeField] private float _projectileVelocity;
		[SerializeField] private LayerMask _hitLayer;

		[Header("Hit時の処理")] [SerializeReference, SubclassSelector]
		private IProjectileHitEffect _projectileHitEffect;
		private EffectSpawner _effectSpawner;
		private Vector3[] _linePositions;
		private int _lastPositionIndex;

		public Vector3 HitPosition => _linePositions[_lastPositionIndex];
		public ReadOnlySpan<Vector3> LinePositions => _linePositions.AsSpan(0, _lastPositionIndex + 1);
		public Vector3 HitNormal { get; private set; }
		[Networked] private ProjectileData CurrentProjectileData { get; set; }

		public struct ProjectileData : INetworkStruct
		{
			public Vector3 StartPosition;
			public Vector3 InitialVelocity;
			public Vector3 CurrentPosition;
			public Vector3 CurrentForward;
			public Vector3 Gravity;
			public float Timer;
			public NetworkBool HasHit;
		}

		public override void Spawned()
		{
			base.Spawned();
			_projectileHitEffect.Initialize();
			_linePositions = new Vector3[(int)(_lifeTime / _simulationStepTime)];
			_effectSpawner = StaticServiceLocator.Instance.Get<EffectSpawner>();
		}

		/// <summary>
		///     投射物を発射する
		/// </summary>
		public void Fire(PlayerRef usePlayerRef)
		{
			CurrentProjectileData = new ProjectileData
			{
				StartPosition = _projectileSpawnPoint.position,
				InitialVelocity = _projectileSpawnPoint.forward * _projectileVelocity,
				CurrentPosition = _projectileSpawnPoint.position,
				Gravity = _gravity,
				Timer = 0f,
				HasHit = false
			};

			Runner.Spawn(_projectilePrefab, _projectileSpawnPoint.position, _projectileSpawnPoint.rotation,
				onBeforeSpawned: (runner, obj) =>
				{
					var projectile = obj.GetComponent<Projectile>();

					RPC_InitializedProjectile(projectile);

					projectile.Fire(CurrentProjectileData, usePlayerRef, (position, rotation, hitObject) =>
					{
						var normal = rotation * Vector3.forward;
						if (Runner.IsServer)
							_projectileHitEffect.OnStateAuthorityHit(position, normal, hitObject,
								usePlayerRef);
						RPC_PlayEffect(position, normal);
					});
				});
			
			_effectSpawner.RequestPlayOneShotEffect(_shootEffectType, _projectileSpawnPoint.position, _projectileSpawnPoint.rotation);
		}

		/// <summary>
		///     放物線の軌道を計算する。
		///     障害物に当たった場合、そこを最終地点とする。
		///     結果は_linePositionsと_lastPositionIndexに保存される。
		/// </summary>
		public void BuildTrajectory()
		{
			for (var i = 0; i < _linePositions.Length; i++)
			{
				// 次の地点の計算
				var time = i * _simulationStepTime;
				var pos = Projectile.CalculateTrajectoryPosition(_projectileSpawnPoint.position,
					_projectileSpawnPoint.transform.forward * _projectileVelocity, _gravity, time);
				_linePositions[i] = pos;

				if (i == 0) continue;
				// 弾が移動予定の位置までRayを飛ばし、当たり判定を確認する
				var ray = new Ray(_linePositions[i - 1], _linePositions[i] - _linePositions[i - 1]);

				// 障害物が存在した場合、その地点を最終地点とする。
				if (Physics.Raycast(ray, out var hit, Vector3.Distance(_linePositions[i - 1], _linePositions[i]), _hitLayer))
				{
					_linePositions[i] = hit.point;
					_lastPositionIndex = i;
					HitNormal = hit.normal;
					return;
				}
			}

			_lastPositionIndex = _linePositions.Length - 1;
			HitNormal = Vector3.up;
		}

		[Rpc]
		private void RPC_InitializedProjectile(Projectile projectile)
		{
			projectile.Initialized(_projectileEffectPrefab.gameObject, _hitLayer);
		}

		[Rpc]
		private void RPC_PlayEffect(Vector3 position, Vector3 normal)
		{
			_projectileHitEffect.OnHit(position, normal);
		}

		#region Gizmos

#if UNITY_EDITOR
		private void OnDrawGizmos()
		{
			if (!Object || !Object.IsValid) return;
			// 当たり判定の可視化
			_projectileHitEffect.DrawGizmos(HitPosition, HitNormal);
		}
#endif
		#endregion
	}

	public interface IProjectileHitEffect
	{
		void Initialize();

		/// <summary>
		///     ProjectileHit時に呼ばれるサーバ上でのゲームロジック処理
		/// </summary>
		void OnStateAuthorityHit(Vector3 hitPos, Vector3 normal, GameObject hitObject, PlayerRef usePlayer);

		/// <summary>
		///     ProjectileHit時に全クライアントで行う処理
		/// </summary>
		void OnHit(Vector3 hitPos, Vector3 normal);

		void DrawGizmos(Vector3 hitPos, Vector3 normal);
	}
}