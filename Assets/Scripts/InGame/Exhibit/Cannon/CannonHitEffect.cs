using Fusion;
using InGame.Health;
using InGame.Player;
using September.Common;
using UnityEngine;

namespace September.InGame.Exhibit
{
	[System.Serializable]
	public class CannonHitEffect : IProjectileHitEffect
	{
		[SerializeField] private ParticleSystem _explosionParticlePrefab;
		[SerializeField] private ParticleSystem _explosionGroundParticlePrefab;
		[SerializeField] private float _radius;
		[SerializeField] private int _damage;
		[SerializeField] private float _knockBackPower = 10;
		[SerializeField] private float _knockBackUpwardPower = 2;
		[SerializeField] private float _knockBackDuration = 0.5f;
		[SerializeField] private LayerMask _hitLayer;
		[SerializeField] private LayerMask _groundLayer;
		[SerializeField] private Vector3 _effectScale;
		private ParticleSystem _explosionParticle;
		private ParticleSystem _explosionGroundParticle;

		public void Initialize()
		{
			_explosionParticle = Object.Instantiate(_explosionParticlePrefab);
			_explosionParticle.transform.localScale = _effectScale;
			_explosionParticle.Stop();
			
			_explosionGroundParticle = Object.Instantiate(_explosionGroundParticlePrefab);
			_explosionGroundParticle.transform.localScale = _effectScale;
			_explosionGroundParticle.Stop();
		}
		
		public void OnHit(Vector3 position, Vector3 normal)
		{
			// ヒット地点のlayerによって再生するパーティクルを変える
			ParticleSystem useParticle=Physics.Raycast(position, -normal, out _, 1, _groundLayer)?
				_explosionGroundParticle : _explosionParticle;
			if (!useParticle) return;
			// 着弾時のエフェクト
			useParticle.transform.position = position;
			useParticle.transform.up = normal.normalized;
			
			useParticle.Play(true);
		}

		public void OnStateAuthorityHit(Vector3 position, Vector3 normal, GameObject hitObject, PlayerRef usePlayer)
		{
			var colliders = Physics.OverlapSphere(position, _radius, _hitLayer); // TODO:当たり判定統一するかも
			// ダメージ処理
			foreach (var col in colliders)
			{
				var damageable = col.GetComponentInParent<IDamageable>();
				if (damageable == null) continue;
				if (damageable.OwnerPlayerRef == usePlayer) continue;
				TakeDamage(damageable, usePlayer);
				KnockBack(col.gameObject, col.transform.position - position);
			}
		}
		
		public void DrawGizmos(Vector3 position, Vector3 normal)
		{
			var colliderPos = position;
			Gizmos.color = Color.green;
			Gizmos.DrawWireSphere(colliderPos, _radius);
		}

		private void TakeDamage(IDamageable damageable, PlayerRef usingPlayer)
		{
			var hitData = new HitData(HitActionType.RangedDamage, _damage, usingPlayer,
				damageable.OwnerPlayerRef);
			PlayerDatabase.Instance.PlayerDataDic.Get(damageable.OwnerPlayerRef);
			PlayerDatabase.Instance.PlayerDataDic.Get(usingPlayer);
			
			damageable.TakeHit(ref hitData);
		}

		private void KnockBack(GameObject obj, Vector3 direction)
		{
			var playerMovement = obj.transform.GetComponentInParent<PlayerMovement>();
			if (playerMovement)
			{
				direction.y = 0;
				direction.Normalize();
				playerMovement.KnockBack(direction * _knockBackPower + Vector3.up * _knockBackUpwardPower,
					_knockBackDuration);
			}
		}
	}
}
