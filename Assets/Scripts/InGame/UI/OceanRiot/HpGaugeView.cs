using DG.Tweening;
using UnityEngine;

namespace September
{
    public class HpGaugeView : MonoBehaviour
    {
        [SerializeField] private Transform _hpGaugeRoot;
        [SerializeField] private DiagonalSliderFill _hpGauge;
        [SerializeField] private DiagonalSliderFill _damageGauge;
        [SerializeField] private float _duration = 0.5f;
        [SerializeField] private float _strength = 20f;
        [SerializeField] private int _vibrate = 100;


        private Sequence _gaugeTween;
        private Tween _shakeTween;
        private Vector3 _shakeOrigin;
        private bool _initialized;
        private bool _playingEmpty;
        private float? _pendingRatio;

        public void Initialize(float ratio)
        {
            StopAnimations();
            _hpGauge.SetFillAmount(ratio);
            _damageGauge.SetFillAmount(ratio);
            _initialized = true;
        }

        /// <summary>
        /// HpBarの演出部分の実装　
        /// </summary>
        /// <param name="targetValue">HPバーが最終的に到達するべき目標値(割合)</param>
        public void SetGauge(float targetValue)
        {
            targetValue = Mathf.Clamp01(targetValue);
            if (_playingEmpty)
            {
                _pendingRatio = targetValue;
                return;
            }
            if (!_initialized || !isActiveAndEnabled)
            {
                Initialize(targetValue);
                return;
            }

            StopAnimations();
            _playingEmpty = targetValue <= 0f;
            _shakeOrigin = _hpGaugeRoot.localPosition;
            _shakeTween = _hpGaugeRoot.DOShakePosition(_duration / 2, _strength, _vibrate);
            _gaugeTween = DOTween.Sequence()
                .Append(_hpGauge.DOFillAmount(targetValue, _duration))
                .Append(_damageGauge.DOFillAmount(targetValue, _duration / 2))
                .OnComplete(() =>
                {
                    _playingEmpty = false;
                    if (_pendingRatio.HasValue)
                    {
                        float ratio = _pendingRatio.Value;
                        _pendingRatio = null;
                        Initialize(ratio);
                    }
                });
        }

        private void StopAnimations()
        {
            _playingEmpty = false;
            _pendingRatio = null;
            _gaugeTween?.Kill();
            _gaugeTween = null;
            if (_shakeTween != null)
            {
                _shakeTween.Kill();
                if (_hpGaugeRoot) _hpGaugeRoot.localPosition = _shakeOrigin;
                _shakeTween = null;
            }
        }

        private void OnDisable() => StopAnimations();
        private void OnDestroy() => StopAnimations();
    }
}
