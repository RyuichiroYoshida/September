using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace September.InGame.NauticalChart
{
    /// <summary> 具体的な霧の処理するクラス </summary>
    public class ConcreteFogController : IFogController
    {
        [Header("Prefab参照")]
        [SerializeField] private GameObject[] _fogPrefab;
        [SerializeField] private ThunderFactory _thunderFactory;
        [SerializeField] private Canvas _canvas;

        [Header("霧の設定")]
        [Tooltip("霧のフェードイン時間"), SerializeField] private float _fogFadeInTime = 0.4f;
        [Tooltip("霧のフェードアウト時間"), SerializeField] private float _fogFadeOutTime = 0.4f;
        [Tooltip("次の霧が表示されるまでの遅延時間"), SerializeField] private float _nextFogInterval = 0.08f;
        [Tooltip("霧のY座標FadeIn位置"), SerializeField] private float _fogFadeInY = 20.0f;
        [Tooltip("霧のY座標FadeOut位置"), SerializeField] private float _fogFadeOutY = -20.5f;

        CancellationTokenSource _cts = new CancellationTokenSource();

        private List<GameObject> _fogInstances = new List<GameObject>();

        /// <summary> 霧のFadeInアニメーション </summary>
        private async UniTaskVoid FogFadeIn()
        {
            for (int i = 0; i < _fogPrefab.Length; i++)
            {
                GameObject obj = Object.Instantiate(_fogPrefab[i], _canvas.transform);
                _fogInstances.Add(obj);
                Image image = obj.GetComponent<Image>();
                RectTransform rect = obj.GetComponent<RectTransform>();
                image.DOFade(1, _fogFadeInTime);
                rect.DOAnchorPosY(rect.anchoredPosition.y, _fogFadeInTime)
                    .From(new Vector2(rect.anchoredPosition.x, _fogFadeInY));
                await UniTask.WaitForSeconds(_nextFogInterval, cancellationToken: _cts.Token);
            }
        }

        /// <summary> 霧のFadeOutアニメーション </summary>
        private async UniTaskVoid FogFadeOut()
        {
            for (int i = 0; i < _fogInstances.Count; i++)
            {
                GameObject obj = _fogInstances[i];
                Image image = obj.GetComponent<Image>();
                RectTransform rect = obj.GetComponent<RectTransform>();
                image.DOFade(0, _fogFadeOutTime);
                rect.DOAnchorPosY(_fogFadeOutY + rect.anchoredPosition.y, _fogFadeOutTime);
                await UniTask.WaitForSeconds(_nextFogInterval, cancellationToken: _cts.Token);
            }
        }

        public void ShowFog()
        {
            FogFadeIn().Forget();
        }

        public void HideFog()
        {
            FogFadeOut().Forget();
        }

        ~ConcreteFogController()
        {
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}
