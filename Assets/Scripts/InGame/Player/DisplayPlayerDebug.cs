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
            PlayerManager playerManager = GetComponentInParent<PlayerManager>();
            PlayerStatus playerStatus = GetComponentInParent<PlayerStatus>();
            _playerMovement = playerStatus.GetComponent<PlayerMovement>();

            if (!playerManager.IsLocalPlayer)
            {
                gameObject.SetActive(false);
                return;
            }

            playerStatus.ReactiveCurrentHealth.Subscribe(health => _healthText.text = health.ToString());
            playerStatus.ReactiveCurrentStamina.Subscribe(stamina => _staminaText.text = stamina.ToString("F1"));
        }

        private void FixedUpdate()
        {
            _speedText.text = $"velo:{_playerMovement.MoveVelocity}\non plane mag:{_playerMovement.GetSpeedOnPlane():F2}";
            _isGroundText.text = $"IsGround:{_playerMovement.IsGround}";
        }
    }
}
