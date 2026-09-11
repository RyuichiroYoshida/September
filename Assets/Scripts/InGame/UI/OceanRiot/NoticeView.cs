using DG.Tweening;
using Fusion;
using InGame.Jewelry;
using UnityEngine;
using UnityEngine.UI;

namespace September
{
    public class NoticeView : MonoBehaviour
    {
        [SerializeField] private SerializableDictionary<NoticeType, Image> _noticeObjects;
        [SerializeField] private float _fadeDuration = 0.5f;
        [SerializeField] private CanvasGroup _canvasGroup;

        private Sequence _noticeSequence;
        private Image _currentNoticeObject;

        private void Awake()
        {
            // 初期状態では全ての通知オブジェクトを非表示にする
            foreach (var noticeObject in _noticeObjects.Dictionary.Values)
            {
                noticeObject.gameObject.SetActive(false);
            }
            _canvasGroup.alpha = 0f;
        }

        public void ShowNotice(float second, NoticeType noticeType)
        {

            if (_noticeObjects.Dictionary.TryGetValue(noticeType, out var noticeObject))
            {
                _noticeSequence?.Kill();
                if (_currentNoticeObject != null && _currentNoticeObject != noticeObject)
                {
                    _currentNoticeObject.gameObject.SetActive(false);
                }

                _currentNoticeObject = noticeObject;
                noticeObject.gameObject.SetActive(true);
                noticeObject.DOKill();
                Color color = noticeObject.color;
                color.a = 0f;
                noticeObject.color = color;
                _canvasGroup.DOKill();
                _canvasGroup.alpha = 0f;

                _noticeSequence = DOTween.Sequence()
                    .Append(DOFade(noticeObject, 1f, _fadeDuration))
                    .Join(_canvasGroup.DOFade(1f, _fadeDuration))
                    .AppendInterval(second - _fadeDuration * 2)
                    .Append(DOFade(noticeObject, 0f, _fadeDuration))
                    .Join(_canvasGroup.DOFade(0f, _fadeDuration))
                    .OnComplete(() =>
                    {
                        noticeObject.gameObject.SetActive(false);
                        if (_currentNoticeObject == noticeObject)
                        {
                            _currentNoticeObject = null;
                            _noticeSequence = null;
                        }
                    });
            }
            else
            {
                Debug.LogError($"[NoticeView] NoticeType {noticeType} not found in dictionary.");
            }

        }

        private Tweener DOFade(Image noticeObject, float targetAlpha, float duration)
        {
            return noticeObject
                .DOFade(targetAlpha, duration)
                .SetEase(Ease.InOutSine);
        }


    }
}
