using September.InGame.Exhibit;
using TMPro;
using UnityEngine;

namespace September
{
    public class BallistaAmmoView : ProjectileAmmoViewBase
    {
        [SerializeField] private TMP_Text _ammoUI;
        [SerializeField] private TMP_Text _reloadText;
        
        public override void UpdateAmmo(int ammo, float coolTime)
        {
            if (ammo <= 0)
            {
                _ammoUI.gameObject.SetActive(false);
                _reloadText.gameObject.SetActive(true);
            }
            else
            {
                _ammoUI.gameObject.SetActive(true);
                _reloadText.gameObject.SetActive(false);
                _ammoUI.text = ammo.ToString();
            }
        }
    }
}
