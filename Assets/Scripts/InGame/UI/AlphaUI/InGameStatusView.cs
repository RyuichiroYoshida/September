using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NaughtyAttributes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace September.InGame.UI
{
    public struct GameTimer
    {
        public int Time;
        public float Duration;
        public float AfterReadyDelay;
        public float GameTime;
    }
    
    /// <summary>UIの管理</summary>
    public class InGameStatusView : MonoBehaviour
    {
        #region インゲーム開始時に生成

        [SerializeField,Label("インゲーム開始時から存在するCanvas")] private GameObject _uiMainCanvasPrefab;
        [SerializeField, Label("オプションUI")] private GameObject _optionUIPrefab;
        [SerializeField, Label("気絶バー")] private UIAnimation _hpBar; 
        [SerializeField,Label("キルログUI")] private UIAnimation _killLogTextPrefab;
        [SerializeField, Label("鬼UI")] private GameObject _ogreUIPrefab;

        #endregion

        #region 途中から生成するUI
        
        [SerializeField,Label("TimerUI")] private TextMeshPro _timerUIPrefab;

        #endregion

        #region AnimationName

        [SerializeField, Label("再生したいHPAnimationの名前")] private string _hpAnimationName;
        [SerializeField,Label("再生したいKillTextの名前")] private string _killAnimationName;

        #endregion

        [SerializeField,Label("経過時間を表示する場所")] private GameObject _timerTransformPosition;

        private Slider _hpBarSlider;
        private TextMeshPro _killLogText;
        private GameObject _optionUI;
        private GameObject _killLogUI;
        private GameTimer _timer;

        private CancellationTokenSource _cts;
        
        private void Start()
        {
            _cts = new CancellationTokenSource();
            Initialize();
        }

        private void Initialize()
        {
            _hpBarSlider = _hpBar.gameObject.GetComponent<Slider>();
            _killLogText = _killLogTextPrefab.gameObject.GetComponent<TextMeshPro>();
            
            if(_hpBarSlider == null)
                Debug.LogError($"{nameof(_hpBarSlider)} is null");
        }
        
        // インゲーム開始時に必要なUIを生成
        public void CreateStartGameUI()
        {
            if (_uiMainCanvasPrefab != null && _optionUI != null && _killLogUI != null)
            {
                GameObject canvas = Instantiate(_uiMainCanvasPrefab);
                _optionUI = Instantiate(_optionUIPrefab, canvas.transform);
                _optionUI.SetActive(false);
                _killLogUI = Instantiate(_killLogTextPrefab.gameObject, canvas.transform);
                _killLogUI.SetActive(false);
                _ogreUIPrefab = Instantiate(_optionUIPrefab, canvas.transform);
                _ogreUIPrefab.SetActive(false);
                
            }
            else
                Debug.LogError("UIPrefab is null");
        }
        
        public void ChangeHp(int value)
        {
            // Hp変更アニメーションを再生
            if(_hpBarSlider != null) 
                _hpBar.Play(_hpAnimationName);
            
            _hpBarSlider.value = value;
        }

        public void ChangeStamina(float value)
        {
            
        }

        // キルのログを直接引数に入れる
        public void ShowKillLog(string killText)
        {
            _killLogText.text = killText;
            
            if(_killAnimationName != null) 
                _killLogTextPrefab.Play(_killAnimationName);
        }
        
        // ToDo : タイマークラスを作成してアニメーションなどを柔軟に行えるようにする
        public async UniTask ShowGameStartTime()
        {
            TextMeshPro timer = Instantiate(_timerUIPrefab);
            timer.gameObject.SetActive(true);
            
            // カウントダウン
            for (int i = _timer.Time; i >= 1; i--)
            {
                timer.text = i.ToString();
                await UniTask.Delay(TimeSpan.FromSeconds(_timer.Duration),cancellationToken: _cts.Token);
            }

            // Ready Goの表示とGameTimeの表示を開始する
            timer.text = "Ready";
            await UniTask.Delay(TimeSpan.FromSeconds(_timer.AfterReadyDelay),cancellationToken: _cts.Token);

            timer.text = "Go!";
            await UniTask.Delay(TimeSpan.FromSeconds(_timer.Duration),cancellationToken: _cts.Token);

            for (float remaining = _timer.GameTime; remaining >= 0; remaining--)
            {
                TimeSpan timeSpan = TimeSpan.FromSeconds(remaining);
                timer.text = timeSpan.ToString(@"mm\:ss");
                await UniTask.Delay(TimeSpan.FromSeconds(1),cancellationToken: _cts.Token);
            }

            timer.text = "Time Up!";
            await UniTask.Delay(TimeSpan.FromSeconds(_timer.Duration),cancellationToken: _cts.Token);
            Destroy(timer.gameObject);
        }

        // 鬼の時にUIを表示する
        public void ShowOgreLamp(bool isShow)
        {
            _ogreUIPrefab.gameObject.SetActive(isShow);
        }
        
        public void ShowOptionUI(bool isShow)
        {
            _optionUI.SetActive(isShow);
        }
    }
}