using DG.Tweening;
using Fusion;
using UnityEngine;
using UnityEngine.Rendering;

namespace September.InGame.NauticalChart
{
    [System.Serializable]
    public struct AmbientSettings
    {
        public AmbientMode ambientMode;
        public Color skyColor;
        public Color equatorColor;
        public Color groundColor;
        [Range(0f, 8f)] public float intensity;
    }

    [System.Serializable]
    public struct DirectionalLightSettings
    {
        public Color color;
        [Range(0f, 8f)] public float intensity;
        [Range(0f, 1f)] public float shadowStrength;
    }

    public class SkyboxChanger : NetworkBehaviour
    {
        private static readonly int Blend = Shader.PropertyToID("_Blend");

        [SerializeField] private Ease _easeType = Ease.InOutSine;
        [SerializeField] private float _duration = 0.5f;

        [Header("Ambient")]
        [SerializeField] private AmbientSettings _normalAmbient;
        [SerializeField] private AmbientSettings _stormAmbient;

        [Header("Directional Light")]
        [SerializeField] private Light _directionalLight;
        [SerializeField] private DirectionalLightSettings _normalLight;
        [SerializeField] private DirectionalLightSettings _stormLight;

        #if UNITY_EDITOR
        [ContextMenu(nameof(Setup))]
        public void Setup()
        {
            UnityEditor.Undo.RecordObject(this, "Reset Skybox");
            _normalAmbient = new AmbientSettings()
            {
                ambientMode = RenderSettings.ambientMode,
                skyColor = RenderSettings.ambientSkyColor,
                equatorColor = RenderSettings.ambientEquatorColor,
                groundColor = RenderSettings.ambientGroundColor,
                intensity = RenderSettings.ambientIntensity
            };

            if (!_directionalLight) return;

            _normalLight = new DirectionalLightSettings()
            {
                color = _directionalLight.color,
                intensity = _directionalLight.intensity,
                shadowStrength = _directionalLight.shadowStrength
            };
        }
        #endif

        /// <summary> Skyboxを変更する </summary>
        public void SkyboxChangeStorm()
        {
            var skybox = RenderSettings.skybox;
            DOTween.To(() => skybox.GetFloat(Blend), x => skybox.SetFloat(Blend, x), 1, _duration).SetEase(_easeType);

            TweenAmbientTo(_stormAmbient);
            TweenLightTo(_stormLight);
        }

        /// <summary>
        /// 元のSkyboxに戻す
        /// </summary>
        public void RestoreSkybox()
        {
            var skybox = RenderSettings.skybox;
            DOTween.To(() => skybox.GetFloat(Blend), x => skybox.SetFloat(Blend, x), 0, _duration).SetEase(_easeType);

            TweenAmbientTo(_normalAmbient);
            TweenLightTo(_normalLight);
        }

        private void TweenAmbientTo(AmbientSettings target)
        {
            RenderSettings.ambientMode = target.ambientMode;

            DOTween.To(
                () => RenderSettings.ambientSkyColor,
                c => RenderSettings.ambientSkyColor = c,
                target.skyColor, _duration).SetEase(_easeType);

            DOTween.To(
                () => RenderSettings.ambientEquatorColor,
                c => RenderSettings.ambientEquatorColor = c,
                target.equatorColor, _duration).SetEase(_easeType);

            DOTween.To(
                () => RenderSettings.ambientGroundColor,
                c => RenderSettings.ambientGroundColor = c,
                target.groundColor, _duration).SetEase(_easeType);

            DOTween.To(
                () => RenderSettings.ambientIntensity,
                v => RenderSettings.ambientIntensity = v,
                target.intensity, _duration).SetEase(_easeType);
        }

        private void TweenLightTo(DirectionalLightSettings target)
        {
            if (_directionalLight == null) return;

            DOTween.To(
                () => _directionalLight.color,
                c => _directionalLight.color = c,
                target.color, _duration).SetEase(_easeType);

            DOTween.To(
                () => _directionalLight.intensity,
                v => _directionalLight.intensity = v,
                target.intensity, _duration).SetEase(_easeType);

            DOTween.To(
                () => _directionalLight.shadowStrength,
                v => _directionalLight.shadowStrength = v,
                target.shadowStrength, _duration).SetEase(_easeType);
        }
    }
}
