using Fusion;
using InGame.Health;
using September.Common;
using September.InGame.Effect;
using UnityEngine;

namespace September.InGame.Exhibit
{
	public class BallistaHitEffect : IProjectileHitEffect
	{
		[SerializeField] private int _baseDamage;
		[SerializeField] private EffectType _effectType;
		private EffectSpawner _spawner;

		public void Initialize()
		{
			_spawner = StaticServiceLocator.Instance.Get<EffectSpawner>();
		}

		public void OnStateAuthorityHit(Vector3 hitPos, Vector3 normal, GameObject hitObject, PlayerRef usePlayer)
		{
			var damageable = hitObject.transform.GetComponentInParent<IDamageable>();
			TakeDamage(damageable, usePlayer);
			
			// このメソッドからRPCメソッドを呼んでいるため、サーバーのみで実行
			_spawner.RequestPlayOneShotEffect(_effectType, hitPos, Quaternion.LookRotation(normal));
		}

		public void OnHit(Vector3 hitPos, Vector3 normal)
		{
		}

		public void DrawGizmos(Vector3 hitPos, Vector3 normal)
		{
			// 
		}

		private void TakeDamage(IDamageable damageable, PlayerRef attackPlayer)
		{
			if (damageable == null) return;
			var hitData = new HitData(HitActionType.RangedDamage, _baseDamage, attackPlayer, damageable.OwnerPlayerRef);
			damageable.TakeHit(ref hitData);
		}
	}
}
