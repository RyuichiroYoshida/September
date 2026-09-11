using TMPro;
using UnityEngine;

namespace September
{
    public class BallistaAmmoView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _ammoUI;
        [SerializeField] private TMP_Text _reloadText;
        
        public void UpdateAmmo(int ammo)
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
