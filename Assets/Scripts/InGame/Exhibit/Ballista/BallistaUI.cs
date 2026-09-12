using September.InGame.Exhibit;
using TMPro;
using UniRx;
using UnityEngine;

namespace  September.InGame.Exhibit.UI
{
    public class BallistaUI : MonoBehaviour
    {
        [SerializeField] ProjectileInteractableBase _projectile;
        [SerializeField] BallistaAmmoView _ammoView;
        private void Start()
        {
            _projectile.OnAmmoChanged += UpdateAmmo;
        }

        private void OnDestroy()
        {
            _projectile.OnAmmoChanged -= UpdateAmmo;
        }

        private void UpdateAmmo(int ammo)
        {
            _ammoView.UpdateAmmo(ammo);
        }
    }
}
