using System;
using UnityEngine;

namespace September.InGame.Exhibit
{
	[Serializable]
	public class BallistaReticle: IReticleEffect
	{
		[SerializeField] private GameObject _reticuleUIGameObject;

		public void Init()
		{
			_reticuleUIGameObject?.SetActive(false);
		}

		public void Render()
		{
			
		}

		public void AllClientEffectActive(bool active)
		{
			
		}

		public void SetActive(bool active)
		{
			_reticuleUIGameObject?.SetActive(active);
		}
	}
}