using System;
using UniRx;

namespace September.InGame.UI
{
    // 各UIのイベントを所持するクラス
    // 登録も自身で行う
    public class UIController : SingletonMonoBehaviour<UIController>
    {
        #region イベント

        private readonly Subject<bool> _onClickOptionButton = new();
        private readonly ReactiveProperty<int> _onChangeSliderValue = new();
        private readonly Subject<Unit> _onStartTimer = new();
        private readonly Subject<string> _onNoticeKillLog = new();
        private readonly ReactiveProperty<bool> _onShowOgreUI = new();
        private readonly ReactiveProperty<int> _onChangeStaminaValue = new();
        private readonly Subject<Unit> _onGameStart = new();

        #endregion
        
        # region 外部公開プロパティ
        
        public IObservable<bool> OnClickOptionButton => _onClickOptionButton;
        public IReadOnlyReactiveProperty<int> OnChangeSliderValue => _onChangeSliderValue;
        public IObservable<Unit> OnStartTimer => _onStartTimer;
        public IObservable<string> OnNoticeKillLog => _onNoticeKillLog;
        public IObservable<bool> OnShowOgreUI => _onShowOgreUI;
        public IReadOnlyReactiveProperty<int> OnChangeStaminaValue => _onChangeStaminaValue;
        
        public IObservable<Unit> OnGameStart => _onGameStart;
        
        #endregion

        public void SetUpStartUI()
        {
            _onGameStart.OnNext(Unit.Default);
        }
        
        public void StartTimer()
        {
            _onStartTimer.OnNext(Unit.Default);
        }

        public void ShowOgreLamp(bool isShow)
        {
            _onShowOgreUI.Value = isShow;
        }

        public void ChangeSliderValue(int value)
        {
            _onChangeSliderValue.Value = value;
        }

        public void ChangeStaminaValue(int value)
        {
            _onChangeStaminaValue.Value = value;
        }
    }
}