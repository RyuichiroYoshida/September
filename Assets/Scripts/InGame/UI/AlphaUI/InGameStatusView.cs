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
        [SerializeField, Label("気絶バー")] private UIAnimation _hpBarPrefab;
        [SerializeField, Label("キルログUI")] private UIAnimation _killLogTextPrefab;
        [SerializeField, Label("鬼UI")] private GameObject _ogreUIPrefab;
        [SerializeField, Label("TimerUI")] private TextMeshProUGUI _timerUIPrefab;

        [Header("Animation Settings")]
        [SerializeField, Label("再生したいHPAnimationの名前")] private string _hpAnimationName;
        [SerializeField, Label("再生したいKillTextの名前")] private string _killAnimationName;

        [Header("Timer Settings")]
        [SerializeField,Label("TimerData")]private GameTimerData _timerData;

        private Slider _hpBarSlider;
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
            ui.OnChangeSliderValue.Subscribe(ChangeHp).AddTo(_cts.Token);
            ui.OnClickOptionButton.Subscribe(ShowOptionUI).AddTo(_cts.Token);
            ui.OnStartTimer.Subscribe(_ => ShowGameStartTime().Forget()).AddTo(_cts.Token);
            ui.OnNoticeKillLog.Subscribe(ShowKillLog).AddTo(_cts.Token);
            ui.OnShowOgreUI.Subscribe(ShowOgreLamp).AddTo(_cts.Token);
            ui.OnChangeStaminaValue.Subscribe(ChangeStamina).AddTo(_cts.Token);
            ui.OnGameStart.Subscribe(_ => SetupUI()).AddTo(_cts.Token);
        }

        private void SetupUI()
        {
            _optionUI = Instantiate(_optionUIPrefab, transform);
            _optionUI.SetActive(false);
            
            _killLogUI = Instantiate(_killLogTextPrefab.gameObject,transform);
            _killLogUI.SetActive(false);
            _killLogText = _killLogUI.GetComponent<TextMeshPro>();
            
            _ogreUiInstance = Instantiate(_ogreUIPrefab, transform);
            _ogreUiInstance.SetActive(false);
            
            _hpBarSlider = Instantiate(_hpBarPrefab.gameObject,transform).GetComponent<Slider>();
            _hpBarSlider.gameObject.SetActive(true);
            
            Debug.Log("UI作成が完了しました");
        }

        private void ChangeHp(int value)
        {
            // Hp変更アニメーションを再生
            if (_hpBarSlider == null)
                return;
            
            _hpBarSlider.value = value;
            _hpBarPrefab.Play(_hpAnimationName);
        }

        private void ChangeStamina(int value)
        {
            // ToDo : 実装予定
        }

        // キルのログを直接引数に入れる
        private void ShowKillLog(string killText)
        {
            if(_killLogText == null)
                return;
            
            _killLogText.text = killText;
            
            if (_killAnimationName != null)
                _killLogTextPrefab.Play(_killAnimationName);
        }

        // ToDo : タイマークラスを作成してアニメーションなどを柔軟に行えるようにする
        private async UniTask ShowGameStartTime()
        {
            TextMeshProUGUI timer = Instantiate(_timerUIPrefab,transform);
            timer.gameObject.SetActive(true);

            // カウントダウン
            for (int i = _timerData.Time; i >= 1; i--)
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