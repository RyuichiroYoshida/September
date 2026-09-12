using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace September
{
    public class TimerView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI[] _timerTexts;
        [SerializeField] private DiagonalSliderFill _timerGauge;
        [SerializeField] private DiagonalSliderFill _timerGaugeRed;
        [SerializeField] private DiagonalSliderFill _timerGaugeWhite;
        private Image _whiteGaugeImageComponent;
        private float _maxTime;
        private bool _isClimax;
        [SerializeField] private float _climaxTimeRatio = 0.1f;
        [SerializeField] private float _flickeringSpeed = 3f;

        public void Initialize(float maxTime)
        {
            _maxTime = Mathf.Max(0f, maxTime);
            _isClimax = false;
            _timerGauge.SetFillAmount(1f);
            _timerGaugeRed.SetFillAmount(1f);
            _timerGaugeWhite.SetFillAmount(1f);

            _timerGauge.gameObject.SetActive(true);
            _timerGaugeRed.gameObject.SetActive(false);
            _timerGaugeWhite.gameObject.SetActive(false);
            _whiteGaugeImageComponent = _timerGaugeWhite.GetComponent<Image>();

            if (_whiteGaugeImageComponent != null)
            {
                Color color = _whiteGaugeImageComponent.color;
                color.a = 0f;
                _whiteGaugeImageComponent.color = color;
            }

            _maxTime = Mathf.Clamp(_maxTime, 0f, _maxTime);
            int minutes = Mathf.FloorToInt(_maxTime / 60f);
            int seconds = Mathf.FloorToInt(_maxTime % 60f);
            SetText($"{minutes:00}{seconds:00}");
        }

        public void SetText(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                if (i < _timerTexts.Length)
                {
                    _timerTexts[i].text = text[i].ToString();
                }
            }
        }

        public void SetTime(float time)
        {
            time = Mathf.Clamp(time, 0f, _maxTime);
            int minutes = Mathf.FloorToInt(time / 60f);
            int seconds = Mathf.FloorToInt(time % 60f);

            SetText($"{minutes:00}{seconds:00}");

            float ratio = _maxTime > 0f
                ? Mathf.Clamp01(time / _maxTime)
                : 0f;

            if (ratio <= _climaxTimeRatio)
            {
                _isClimax = true;
                _timerGaugeRed.gameObject.SetActive(true);
                _timerGauge.gameObject.SetActive(false);
                _timerGaugeWhite.gameObject.SetActive(true);

                _timerGaugeWhite.transform.SetAsLastSibling();

                _timerGaugeRed.SetFillAmount(ratio);
                _timerGaugeWhite.SetFillAmount(ratio);
                return;
            }

            _isClimax = false;
            _timerGauge.gameObject.SetActive(true);
            _timerGaugeRed.gameObject.SetActive(false);
            _timerGaugeWhite.gameObject.SetActive(false);
            _timerGauge.SetFillAmount(ratio);
        }

        private void Update()
        {
            if (!_isClimax || _whiteGaugeImageComponent == null) return;
            Color color = _whiteGaugeImageComponent.color;
            color.a = Mathf.PingPong(Time.time * _flickeringSpeed, 1f);
            _whiteGaugeImageComponent.color = color;
        }
    }
}
