using NaughtyAttributes;
using September.Common;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace September.InGame.Decal
{
    [RequireComponent(typeof(DecalProjector))]
    public class URPDecalController : MonoBehaviour
    {
        [Label("URP Decal Projector")]
        [SerializeField] private DecalProjector _urpDecalProjector;
        [SerializeField] private AnimationCurve _fadeCurve = AnimationCurve.Linear(0, 1, 1, 0);
        [SerializeField] private MinMaxRange _randomLifeTimeScale = new(1f, 1f);

        private float _startTime;
        private float _lifeTimeScale;

        private void OnEnable()
        {
            _startTime = Time.time;
            _lifeTimeScale = 1f / Mathf.Lerp(_randomLifeTimeScale.Min, _randomLifeTimeScale.Max, Random.value);
        }

        private void Update()
        {
            float t = _fadeCurve.Evaluate((Time.time - _startTime) * _lifeTimeScale);
            float fadeFactor = Mathf.Lerp(0f, 1f, t);
            _urpDecalProjector.fadeFactor = fadeFactor;
        }

        private void Reset()
        {
            _urpDecalProjector = GetComponent<DecalProjector>();
        }
    }
}
