using System;
using System.Threading;
using Cysharp.Threading.Tasks;
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

        [Header("Canvas Prefabs")] 
        [SerializeField, Label("MainCanvas")] private Canvas _mainCanvas;
        [SerializeField, Label("OptionCanvas")] private Canvas _optionCanvas;
        
        [Header("Timer Settings")]
        [SerializeField,Label("TimerData")]private GameTimerData _timerData;

        private Slider _hpBarSlider;
        private Slider _staminaBarSlider;
        private TextMeshPro _killLogText;
        private GameObject _optionUI;
        private GameObject _killLogUI;
        private GameObject _ogreUiInstance;

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
            ui.OnNoticeKillLog.Subscribe(ShowKillLog).AddTo(_cts.Token);
            ui.OnShowOgreUI.Subscribe(ShowOgreLamp).AddTo(_cts.Token);
            ui.OnChangeStaminaValue.Skip(1).Subscribe(ChangeStamina).AddTo(_cts.Token);
        }

        private void SetupUI()
        {
            _optionUI = Instantiate(_optionUIPrefab, _optionCanvas.transform);
            _optionUI.SetActive(true);
            
            _killLogUI = Instantiate(_killLogTextPrefab.gameObject, _mainCanvas.transform);
            _killLogUI.SetActive(false);
            _killLogText = _killLogUI.GetComponent<TextMeshPro>();
            
            _ogreUiInstance = Instantiate(_ogreUIPrefab, _mainCanvas.transform);
            _ogreUiInstance.SetActive(false);
            
            _hpBarSlider = Instantiate(_hpBarPrefab.gameObject,_mainCanvas.transform).GetComponent<Slider>();
            _hpBarSlider.gameObject.SetActive(true);
            
            _staminaBarSlider = Instantiate(_staminaBarPrefab.gameObject,_mainCanvas.transform).GetComponent<Slider>();
            _staminaBarSlider.gameObject.SetActive(true);
        }

        private void ChangeHp(int value)
        {
            // Hp変更アニメーションを再生
            if (_hpBarSlider == null)
                return;
            
            _hpBarSlider.value = value;
        }

        private void ChangeStamina(float value)
        {
            _staminaBarSlider.value = value;
        }

        // キルのログを直接引数に入れる
        private void ShowKillLog(string killText)
        {
            if(_killLogText == null)
                return;
            
            _killLogText.text = killText;
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