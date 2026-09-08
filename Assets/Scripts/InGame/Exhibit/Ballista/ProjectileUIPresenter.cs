using UnityEngine;

namespace September.InGame.Exhibit
{
	public class ProjectileUIPresenter : MonoBehaviour
	{
		[SerializeField] private ProjectileInteractableBase _projectile;
		[SerializeField] private ProjectileAmmoViewBase _ammoView;

		private void Start()
		{
			_projectile.OnAmmoChanged += UpdateAmmo;
		}

		private void OnDestroy()
		{
			_projectile.OnAmmoChanged -= UpdateAmmo;
		}

		private void UpdateAmmo(int ammo, float coolTime)
		{
			_ammoView.UpdateAmmo(ammo, coolTime);
		}
	}

	public abstract class ProjectileAmmoViewBase : MonoBehaviour
	{
		public abstract void UpdateAmmo(int ammo, float coolTime);
	}
}