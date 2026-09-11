using System;
using Cysharp.Threading.Tasks;
using Fusion;
using InGame.Exhibit;
using InGame.Jewelry;
using UniRx;
using UnityEngine;

namespace September.InGame.UI
{
    // 各UIのイベントを所持するクラス
    // 登録も自身で行う
    public class UIController : SingletonMonoBehaviour<UIController>
    {
        #region イベント

        private readonly Subject<bool> _onClickOptionButton = new();
        private readonly Subject<ControlDescriptionType> _onChangeDescriptionUI = new();
        private readonly ReactiveProperty<float> _onHealthRatioChanged = new();
        private readonly Subject<NetworkRunner> _onStartTimer = new();
        private readonly Subject<string> _onShowLog = new();
        private readonly ReactiveProperty<bool> _onShowOgreUI = new();
        private readonly Subject<int> _changeTagNoticeObserver = new();
        private readonly Subject<int> _onchangeScoreText = new();
        private readonly ReactiveProperty<float> _onChangeStaminaValue = new();
        private readonly Subject<Unit> _onGameStart = new();
        private readonly Subject<Unit> _onGameEnd = new();
        private readonly Subject<(bool, GameObject)> _isInteracting = new();
        private readonly ReactiveProperty<float> _onChangeInteractProgress = new();
        private readonly Subject<(float, StatusUpType)> _onInteractStatusUpObject = new();
        private readonly Subject<bool> _onOutField = new();
        private readonly Subject<(float, NoticeType)> _onNotice = new();
        private readonly Subject<int> _onEvasionStaminaRecoverd = new();
        private readonly Subject<float> _onEvasionStaminaProgressChanged = new();

        #endregion

        #region 外部公開プロパティ

        public IObservable<bool> OnClickOptionButton => _onClickOptionButton;
        public IReadOnlyReactiveProperty<float> OnHealthRatioChanged => _onHealthRatioChanged;
        public IObservable<NetworkRunner> OnStartTimer => _onStartTimer;
        public IObservable<string> OnShowLog => _onShowLog;
        public IObservable<bool> OnShowOgreUI => _onShowOgreUI;
        public IObservable<int> ChangeTagNoticeObserver => _changeTagNoticeObserver;
        public IReadOnlyReactiveProperty<float> OnChangeStaminaValue => _onChangeStaminaValue;
        public IObservable<ControlDescriptionType> OnChangeDescriptionUI => _onChangeDescriptionUI;
        public IObservable<Unit> OnGameStart => _onGameStart;
        public IObservable<Unit> OnGameEnd => _onGameEnd;
        public IObservable<(bool, GameObject)> IsInteracting => _isInteracting;
        public IReadOnlyReactiveProperty<float> OnChangeInteractProgress => _onChangeInteractProgress;
        public IObservable<(float, StatusUpType)> OnInteractStatusUpObject => _onInteractStatusUpObject;
        public Func<TimeMessageType, UniTask> TimeOverlayMessage { get; set; }
        public IObservable<int> OnChangeScoreText => _onchangeScoreText;
        public IObservable<bool> OnOutField => _onOutField;
        public IObservable<(float, NoticeType)> OnNotice => _onNotice;
        public IObservable<int> OnEvasionStaminaChanged => _onEvasionStaminaRecoverd;
        public IObservable<float> OnEvasionStaminaProgressChanged => _onEvasionStaminaProgressChanged;
        #endregion

        public InGameUIRootRefs UIRootRefs { get; set; }

        public void SetUpStartUI()
        {
            _onGameStart.OnNext(Unit.Default);
        }

        public void ShowResultAnimation()
        {
            _onGameEnd.OnNext(Unit.Default);
        }

        public void OnChangeScore(int score)
        {
            _onchangeScoreText.OnNext(score);
        }

        public void ShowLog(string text)
        {
            Debug.Log($"[InGameLog][UIController][aaa] ShowLog: {text}", this);
            _onShowLog.OnNext(text);
        }

        public void ChangeDescriptionUI(ControlDescriptionType type)
        {
            _onChangeDescriptionUI.OnNext(type);
        }

        public void StartTimer(NetworkRunner runner)
        {
            _onStartTimer.OnNext(runner);
        }

        public void ShowOgreLamp(bool isShow)
        {
            _onShowOgreUI.Value = isShow;
        }

        public void ChangeTagNotice(int messageType)
        {
            _changeTagNoticeObserver.OnNext(messageType);
        }

        public void ChangeHealthRatio(float ratio)
        {
            bool isFirstValue = !HasHealthRatio;
            HasHealthRatio = true;
            if (isFirstValue)
                _onHealthRatioChanged.SetValueAndForceNotify(Mathf.Clamp01(ratio));
            else
                _onHealthRatioChanged.Value = Mathf.Clamp01(ratio);
        }

        public bool HasHealthRatio { get; private set; }

        public void ChangeStaminaValue(float value)
        {
            _onChangeStaminaValue.Value = value;
        }
        public void ShowInteractUI(bool isShow, GameObject target = null)
        {
            _isInteracting.OnNext((isShow, target));
        }

        public void SetInteractProgress(float progress)
        {
            _onChangeInteractProgress.Value = progress;
            if (progress >= 1.0f)
            {
                _isInteracting.OnNext((false, null));
            }
        }

        public void ShowStatusUpUI(float seconds, StatusUpType status)
        {
            _onInteractStatusUpObject.OnNext((seconds, status));
        }

        public void ShowOutFieldUI(bool isActive)
        {
            _onOutField.OnNext(isActive);
        }

        public void ShowNotice(float second, NoticeType noticeType)
        {
            _onNotice.OnNext((second, noticeType));
        }

        public void ShowEvasionStamina(int value)
        {
            _onEvasionStaminaRecoverd.OnNext(value);
        }

        public void ShowEvasionStaminaProgress(float value)
        {
            _onEvasionStaminaProgressChanged.OnNext(value);
        }
    }
}
