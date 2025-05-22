using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UniRx;

namespace September.InGame.UI
{
    // 各UIのイベントを所持するクラス
    // 登録も自身で行う
    public class UIController : SingletonMonoBehaviour<UIController>
    {
        [SerializeField] private InGameStatusView _inGameStatusView;
        
        #region イベント

        private readonly Subject<bool> _onClickOptionButton = new();
        private readonly ReactiveProperty<int> _onChangeSliderValue = new();
        private readonly Subject<Unit> _onStartTimer = new();
        private readonly Subject<string> _onNoticeKillLog = new();
        private readonly Subject<bool> _onShowOgreUI = new();
        private readonly ReactiveProperty<float> _onChangeStaminaValue = new();

        #endregion
        
        # region 外部公開プロパティ
        
        public IObservable<bool> OnClickOptionButton => _onClickOptionButton;
        public ReactiveProperty<int> OnOnChangeSliderValue => _onChangeSliderValue;
        
        public IObservable<Unit> OnStartTimer => _onStartTimer;
        
        public IObservable<string> OnNoticeKillLog => _onNoticeKillLog;
        
        public IObservable<bool> OnShowOgreUI => _onShowOgreUI;
        
        public ReactiveProperty<float> OnChangeStaminaValue => _onChangeStaminaValue;
        
        #endregion

        private CancellationTokenSource _cts;

        private void Start()
        {
            _cts = new CancellationTokenSource();
            Instantiate(_inGameStatusView.gameObject);
        }

        private void Initialize()
        {
            OnOnChangeSliderValue.Subscribe(hp => _inGameStatusView.ChangeHp(hp)).AddTo(_cts.Token);
            OnClickOptionButton.Subscribe(isShow => _inGameStatusView.ShowOptionUI(isShow)).AddTo(_cts.Token);
            OnStartTimer.Subscribe(_=> _inGameStatusView.ShowGameStartTime().Forget()).AddTo(_cts.Token);
            OnNoticeKillLog.Subscribe( killText => _inGameStatusView.ShowKillLog(killText)).AddTo(_cts.Token);
            OnShowOgreUI.Subscribe(isShow => _inGameStatusView.ShowOgreLamp(isShow)).AddTo(_cts.Token);
            OnChangeStaminaValue.Subscribe(stamina => _inGameStatusView.ChangeStamina(stamina)).AddTo(_cts.Token);
        }

        // UIを動的に生成する
        public void GameStart()
        {
            if (_inGameStatusView != null)
            {
                Initialize();
                _inGameStatusView.CreateStartGameUI();
            }
            else
                Debug.LogWarning("UIView is null");
        }

        // ローカルで時間UIを表示する
        public void StartTimer()
        {
            _onStartTimer.OnNext(Unit.Default);
        }

        private void OnDestroy()
        {
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}