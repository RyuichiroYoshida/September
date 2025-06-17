using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using NaughtyAttributes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UniRx;

namespace September.InGame.UI
{
    /// <summary>UIの管理</summary>
    public class InGameStatusView : MonoBehaviour
    {
        [Header("UI Prefabs")]
        [SerializeField, Label("オプションUI")] private GameObject _optionUIPrefab;
        [SerializeField, Label("気絶バー")] private GameObject _hpBarPrefab;
        [SerializeField, Label("キルログUI")] private GameObject _killLogTextPrefab;
        [SerializeField, Label("鬼UI")] private GameObject _ogreUIPrefab;
        [SerializeField, Label("TimerUI")] private TextMeshProUGUI _timerUIPrefab;
        [SerializeField, Label("スタミナUI")] private GameObject _staminaBarPrefab;
        [SerializeField, Label("インタラクトUI")] private GameObject _interactUIPrefab;

        [Header("Canvas Prefabs")] 
        [SerializeField, Label("MainCanvas")] private Canvas _mainCanvas;
        [SerializeField, Label("OptionCanvas")] private Canvas _optionCanvas;
        
        [Header("Timer Settings")]
        [SerializeField,Label("TimerData")] private GameTimerData _timerData;

        //[Header("UI Positions")] [SerializeField, Label("鬼ランプの表示Position")] private Vector2 _ogreUIPosition;

        private Slider _hpBarSlider;
        private Slider _staminaBarSlider;
        private TextMeshProUGUI _killLogText;
        private GameObject _optionUI;
        private GameObject _killLogUI;
        private GameObject _ogreUiInstance;
        private InteractUi _interactUI;

        private CancellationTokenSource _cts;

        private void Awake()
        {
            _cts = new CancellationTokenSource();
            Bind();
        }

        private void Bind()
        {
            UIController ui = UIController.I;
            ui.OnGameStart.Subscribe(_ => SetupUI()).AddTo(_cts.Token);
            ui.OnChangeSliderValue.Subscribe(ChangeHp).AddTo(_cts.Token);
            ui.OnClickOptionButton.Subscribe(ShowOptionUI).AddTo(_cts.Token);
            ui.OnStartTimer.Subscribe(_ => ShowGameStartTime().Forget()).AddTo(_cts.Token);
            ui.OnNoticeKillLog.Subscribe(killText => ShowKillLog(killText).Forget()).AddTo(_cts.Token);
            ui.OnShowOgreUI.Subscribe(ShowOgreLamp).AddTo(_cts.Token);
            ui.OnChangeStaminaValue.Skip(1).Subscribe(ChangeStamina).AddTo(_cts.Token);
            ui.IsInteracting.Subscribe(isInteracting => _interactUI?.SetActive(isInteracting.Item1, isInteracting.Item2)).AddTo(_cts.Token);
            ui.OnChangeInteractProgress.Subscribe(progress => _interactUI?.SetInteractProgress(progress)).AddTo(_cts.Token);
        }

        private void SetupUI()
        {
            _optionUI = Instantiate(_optionUIPrefab, _optionCanvas.transform);
            _optionUI.SetActive(true);
            
            _killLogUI = Instantiate(_killLogTextPrefab.gameObject, _mainCanvas.transform);
            _killLogUI.SetActive(false);
            _killLogText = _killLogUI.GetComponent<TextMeshProUGUI>();
            if(_killLogText == null)
                Debug.LogWarning("_killLogText is null");
            
            _ogreUiInstance = Instantiate(_ogreUIPrefab, _mainCanvas.transform);
            //RectTransform rectTransform = _ogreUiInstance.GetComponent<RectTransform>();
            //rectTransform.anchoredPosition = _ogreUIPosition;
            _ogreUiInstance.SetActive(false);
            
            _hpBarSlider = Instantiate(_hpBarPrefab.gameObject,_mainCanvas.transform).GetComponent<Slider>();
            _hpBarSlider.gameObject.SetActive(true);
            
            _staminaBarSlider = Instantiate(_staminaBarPrefab.gameObject,_mainCanvas.transform).GetComponent<Slider>();
            _staminaBarSlider.gameObject.SetActive(true);
            
            _interactUI = Instantiate(_interactUIPrefab, _mainCanvas.transform).GetComponent<InteractUi>();
            _interactUI.SetActive(false);
        }

        private void ChangeHp(int value)
        {
            if (_hpBarSlider == null)
                return;

            DOTween.To(() => _hpBarSlider.value, x => _hpBarSlider.value = x, value, 0.3f)
                .SetEase(Ease.OutQuad);
        }

        private void ChangeStamina(float value)
        {
            _staminaBarSlider.value = value;
        }

        // キルのログを直接引数に入れる
        private async UniTask ShowKillLog(string killText)
        {
            if(_killLogText == null)
                return;
            
            _killLogText.text = killText;
            _killLogUI.SetActive(true);
            
            RectTransform rect = _killLogText.GetComponent<RectTransform>();
            CanvasGroup canvasGroup = _killLogText.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = _killLogText.gameObject.AddComponent<CanvasGroup>();

            // 初期状態（下側、完全表示）
            rect.anchoredPosition = new Vector2(0, 0);
            canvasGroup.alpha = 1f;

            float moveDistance = 100f;
            float duration = 2f;
            float fadeDuration = 0.5f;

            // DoTweenアニメーションを開始
            Sequence seq = DOTween.Sequence();
            seq.Append(rect.DOAnchorPosY(moveDistance, duration).SetEase(Ease.OutCubic));
            seq.Join(canvasGroup.DOFade(0f, fadeDuration).SetDelay(duration - fadeDuration));
            // DoTweenが完了するまで待機
            await seq.AsyncWaitForCompletion();

            // 完了後に削除 or 非表示
            _killLogText.gameObject.SetActive(false); 
        }

        // ToDo : タイマークラスを作成してアニメーションなどを柔軟に行えるようにする
        private async UniTask ShowGameStartTime()
        {
            TextMeshProUGUI timer = Instantiate(_timerUIPrefab,transform);
            timer.gameObject.SetActive(true);

            // カウントダウン
            for (int i = _timerData.PreStartTime; i >= 1; i--)
            {
                timer.text = i.ToString();
                await UniTask.Delay(TimeSpan.FromSeconds(_timerData.Duration), cancellationToken: _cts.Token);
            }

            // Ready Goの表示とGameTimeの表示を開始する
            timer.text = "Ready";
            await UniTask.Delay(TimeSpan.FromSeconds(_timerData.AfterReadyDelay), cancellationToken: _cts.Token);

            timer.text = "Go!";
            await UniTask.Delay(TimeSpan.FromSeconds(_timerData.Duration), cancellationToken: _cts.Token);

            for (float remaining = _timerData.GameTime; remaining >= 0; remaining--)
            {
                timer.text = TimeSpan.FromSeconds(remaining).ToString(@"mm\:ss");
                await UniTask.Delay(TimeSpan.FromSeconds(1), cancellationToken: _cts.Token);
            }

            timer.text = "Time Up!";
            await UniTask.Delay(TimeSpan.FromSeconds(_timerData.Duration), cancellationToken: _cts.Token);
            Destroy(timer.gameObject);
        }

        // 鬼の時にUIを表示する
        private void ShowOgreLamp(bool isShow)
        {
            if(_ogreUiInstance != null) 
                _ogreUiInstance.gameObject.SetActive(isShow);
        }

        private void ShowOptionUI(bool isShow)
        {
            if(_optionUI != null) 
                _optionUI.SetActive(isShow);
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
    }
}