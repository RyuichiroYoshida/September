using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace September.Common
{
    public class LoadingIcon : MonoBehaviour
    {
        [SerializeField] Image[] _images;
        [SerializeField] float _animationTime = 2f;
        CancellationTokenSource _cts;

        private void Awake()
        {
            InitializeImage();
        }

        void InitializeImage()
        {
            foreach (var image in _images)
            {
                image.transform.localScale = Vector3.zero;
                var color = image.color;
                color.a = 1;
                image.color = color;
            }
        }
        public void StartAnimation()
        {
            if (_cts is { IsCancellationRequested: false }) return;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            InitializeImage();

            var delay = _animationTime / _images.Length;
            for (int i = 0; i < _images.Length; i++)
            {
                _images[i].transform.localScale = Vector3.one;
                _images[i].DOFade(0, _animationTime).SetLoops(-1, LoopType.Restart).SetUpdate(true).SetDelay(i * delay).ToUniTask(cancellationToken: _cts.Token);
                _images[i].transform.DOScale(0, _animationTime).SetLoops(-1, LoopType.Restart).SetUpdate(true).SetDelay(i * delay).ToUniTask(cancellationToken: _cts.Token);
            }
        }


        public void StopAnimation()
        {
            _cts?.Cancel();
            InitializeImage();
        }
    }
}