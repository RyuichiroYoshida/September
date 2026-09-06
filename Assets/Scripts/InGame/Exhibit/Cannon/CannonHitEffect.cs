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
		[SerializeField] private float _radius;
		[SerializeField] private int _damage;
		[SerializeField] private float _knockBackPower = 10;
		[SerializeField] private float _knockBackUpwardPower = 2;
		[SerializeField] private float _knockBackDuration = 0.5f;
		[SerializeField] private LayerMask _hitLayer;
		private ParticleSystem _explosionParticle;

		public void Initialize()
		{
			_explosionParticle = Object.Instantiate(_explosionParticlePrefab);
			_explosionParticle.Stop();
		}
		
		public void OnHit(Vector3 position, Vector3 normal)
		{
			// 着弾時のエフェクト
			_explosionParticle.transform.position = position;
			_explosionParticle.transform.up = normal.normalized;
			
			if (!_explosionParticle) return;
			_explosionParticle.Play(true);
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
