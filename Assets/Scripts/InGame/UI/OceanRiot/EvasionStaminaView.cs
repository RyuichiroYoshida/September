using September.InGame.UI;
using InGame.Player;
using UnityEngine;

namespace September
{
    public class EvasionStaminaView : MonoBehaviour
    {
        [SerializeField] private DiagonalSliderFill _gaugeView;
        [SerializeField]private DiagonalSliderFill _recoverGaugeView;

        public void SetRecoverGaugeProgress(float staminaValue)
        {
            float ratio = Mathf.Clamp01(staminaValue / PlayerMovement.MaxEvasionStamina);
            _recoverGaugeView.SetFillAmount(ratio);
        }
        public void SetEvasionStaminaGauge(float staminaValue)
        {
            float ratio = Mathf.Clamp01(staminaValue / PlayerMovement.MaxEvasionStamina);
            _gaugeView.SetFillAmount(ratio);
            _recoverGaugeView.SetFillAmount(ratio);
        }
    }
}
