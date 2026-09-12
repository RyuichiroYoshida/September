using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace September.InGame.Exhibit.Glider
{
	[Serializable]
	public class GlidreReticle : IReticleEffect
	{
		[SerializeField] ProjectileLauncher _launcher;
		[SerializeField] private GameObject _gliderUI;
		[SerializeField] GameObject _reticleEffectPrefab;
		[SerializeField] private float _reticleRadius = 3;
		[SerializeField] private float _aimPositionOffset = 0.2f;
		private GameObject _reticleEffect;
		public void Init()
		{
			if(!_reticleEffect)
			{
				_reticleEffect = Object.Instantiate(_reticleEffectPrefab);
				_reticleEffect.SetActive(false);
			}
			var size = _reticleRadius * 2;
			_reticleEffect.transform.localScale = new Vector3(size, size, size);
			
			_gliderUI.SetActive(false);
		}

		public void Render()
		{
			_launcher.BuildTrajectory();
			_reticleEffect.transform.position =
				_launcher.HitPosition + _launcher.HitNormal * _aimPositionOffset;
			_reticleEffect.transform.up = _launcher.HitNormal;
		}

		public void SetActive(bool active)
		{
			_gliderUI.SetActive(active);
		}

		public void AllClientEffectActive(bool active)
		{
			_reticleEffect?.SetActive(active);
		}
	}
}