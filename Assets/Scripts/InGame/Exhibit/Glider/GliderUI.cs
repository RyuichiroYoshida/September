using Cysharp.Threading.Tasks;
using September.InGame.Exhibit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace September.InGame.Exhibit
{
	public class GliderUI : ProjectileAmmoViewBase
	{
		[SerializeField] private Image _coolTimeImage;
		public override void UpdateAmmo(int ammo, float coolTime)
		{
			ReloadUiAnim(ammo, coolTime).Forget();
		}

		private async UniTask ReloadUiAnim(int ammo, float coolTime)
		{
			var timer = coolTime;
			if (ammo == 0)
			{
				while (timer > 0)
				{
					await UniTask.WaitForFixedUpdate();
					timer -= Time.fixedDeltaTime;
					_coolTimeImage.fillAmount = 1 - timer / coolTime;
				}
			}
		}
	}
}