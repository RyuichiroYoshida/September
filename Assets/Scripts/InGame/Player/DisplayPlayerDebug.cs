using TMPro;
using UniRx;
using UnityEngine;

namespace InGame.Player
{
    // デバッグ用にPlayer情報をUIに出す
    public class DisplayPlayerDebug : MonoBehaviour
    {
        [SerializeField] TMP_Text _healthText;
        [SerializeField] TMP_Text _staminaText;
        
        private void Start()
        {
            PlayerData playerData = GetComponentInParent<PlayerData>();

            if (!playerData.IsLocalPlayer)
            {
                gameObject.SetActive(false);
                return;
            }
            
            playerData.Health.Subscribe(health => _healthText.text = health.ToString());
            playerData.StaminaSubject.Subscribe(stamina => _staminaText.text = stamina.ToString("F1"));
        }
    }
}
