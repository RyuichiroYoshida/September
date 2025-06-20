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
        [SerializeField] TMP_Text _speedText;
        [SerializeField] TMP_Text _isGroundText;

        PlayerMovement _playerMovement;
        
        private void Start()
        {
            PlayerStatus playerStatus = GetComponentInParent<PlayerStatus>();
            _playerMovement = playerStatus.GetComponent<PlayerMovement>();

            if (!playerStatus.ISLocalPlayer)
            {
                gameObject.SetActive(false);
                return;
            }

            playerStatus.CurrentHealth.Subscribe(health => _healthText.text = health.ToString());
            playerStatus.CurrentStamina.Subscribe(stamina => _staminaText.text = stamina.ToString("F1"));
        }

        private void FixedUpdate()
        {
            Vector3 xzVelo = _playerMovement.MoveVelocity;
            xzVelo.y = 0;
            _speedText.text = $"velo:{_playerMovement.MoveVelocity}\nmag:{xzVelo.magnitude:F2}";
            _isGroundText.text = $"IsGround:{_playerMovement.IsGround}";
        }
    }
}
