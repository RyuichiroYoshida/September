using System;
using UnityEngine;

namespace September.InGame.Exhibit
{
	[Serializable]
	public class CannonReticle : IReticleEffect
	{
		[SerializeField] private ProjectileLauncher _projectileLauncher;
		[SerializeField] private GameObject _aimPositionEffectPrefab;
		[SerializeField] private LineRenderer _lineRenderer;
		[SerializeField] private float _hitReticuleRadius;
		[SerializeField] private float _aimPositionOffset = 0.2f;
		private GameObject _aimPositionEffect;
		private bool _lineActive = false;
		private bool _aimObjectActive = false;
		
		public void Init()
		{
			var size = _hitReticuleRadius * 2;
			if(!_aimPositionEffect)
			{
				_aimPositionEffect = GameObject.Instantiate(_aimPositionEffectPrefab);
				_aimPositionEffect.SetActive(false);
			}
			_aimPositionEffect.transform.localScale = new Vector3(size, size, size);
			_aimPositionEffect.SetActive(false);
			
			LineReset();
		}

		public void Render()
		{
			_projectileLauncher.BuildTrajectory();
			if(_aimObjectActive)
			{
				_aimPositionEffect.transform.position =
					_projectileLauncher.HitPosition + _projectileLauncher.HitNormal * _aimPositionOffset;
				_aimPositionEffect.transform.up = _projectileLauncher.HitNormal;
			}
			
			if(_lineActive)
			{
				var span = _projectileLauncher.LinePositions;
				_lineRenderer.positionCount = span.Length;
				_lineRenderer.SetPositions(span.ToArray());
			}
		}

		public void SetActive(bool active)
		{
			_lineActive = active;
			if(!active)
			{
				LineReset();
			}
		}

		public void AllClientEffectActive(bool active)
		{
			_aimObjectActive = active;
			_aimPositionEffect?.SetActive(active);
		}

		public void LineReset()
		{
			_lineRenderer.positionCount = 0;
		}
	}
}