using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Fusion;
using InGame.Exhibit;
using NaughtyAttributes;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace September.InGame.UI
{
    /// <summary>UIの管理</summary>
    public class InGameStatusView : MonoBehaviour
    {
        [Header("UI Root Prefab")]
        [SerializeField, Label("InGameUIRoot")] private InGameUIRootRefs _inGameUiRootPrefab;
        // [SerializeField] private ResultUIRootRefs _resultUIRootPrefab;

        [Header("Canvas")]
        [SerializeField, Label("MainCanvas")] private Canvas _mainCanvas;

        [Header("Timer Settings")]
        [SerializeField, Label("TimerData")] private GameTimerData _timerData;

        [Header("キルログ")]
        [SerializeField] private GameObject _killLogItemText;
        [SerializeField] private int _maxLogCount = 5;

        [SerializeField] private ControlsUIGenerator _controlsUIGenerator;

        [SerializeField] private CanvasGroup _statusUpUI;
        private VerticalLayoutGroup _statusUpLayout;

        private InGameUIRootRefs _uiRoot;
        private HpGaugeView _hpBarSlider;
        private Slider _staminaBarSlider;
        private readonly Queue<GameObject> _killLogQueue = new();
        private GameObject _optionUI;
        private GameObject _LogPanel;
        private GameObject _ogreUiInstance;
        private ChangeTagOverlayMessage _changeTagOverlayMessage;
        private TimeOverlayMessage _timeOverlayMessage;
        private InteractUi _interactUI;
        private UniTask _ogreMessageTask;
        private TextMeshProUGUI _scoreText;
        private CancellationTokenSource _cts;
        private StatusUpType _currentStatusUpType;
        private CanvasGroup _ogreGroup;
        private CanvasGroup _fieldOutUI;
        private NoticeView _noticeView;

        public InGameUIRootRefs UIRoot => _uiRoot;

        private void Awake()
        {
            _cts = new CancellationTokenSource();
            Bind();
        }

        private void Bind()
        {
            UIController ui = UIController.I;
            ui.OnGameStart.Subscribe(_ => SetupUI()).AddTo(_cts.Token);

            ui.OnHealthRatioChanged.Subscribe(ChangeHp).AddTo(_cts.Token);
            ui.OnClickOptionButton.Subscribe(ShowOptionUI).AddTo(_cts.Token);
            ui.OnStartTimer.Subscribe(runner => ShowGameStartTime(runner).Forget()).AddTo(_cts.Token);
            ui.OnShowLog.Subscribe(killText => ShowLog(killText).Forget()).AddTo(_cts.Token);
            ui.OnShowOgreUI.Subscribe(ShowOgreLamp).AddTo(_cts.Token);
            //  Bind前に_uiRootが生成されないのでChangeTagNoticeを直接Subscribeできない
            ui.ChangeTagNoticeObserver.Subscribe(index => _changeTagOverlayMessage.ChangeTagNotice(index)).AddTo(_cts.Token);
            ui.OnChangeStaminaValue.Skip(1).Subscribe(ChangeStamina).AddTo(_cts.Token);
            // ui.OnGameEnd.Subscribe(_ => PlayResultAnimation().Forget()).AddTo(_cts.Token);
            ui.IsInteracting
                .Subscribe(isInteracting => _interactUI?.SetActive(isInteracting.Item1, isInteracting.Item2))
                .AddTo(_cts.Token);
            ui.OnChangeInteractProgress.Subscribe(progress => _interactUI?.SetInteractProgress(progress))
                .AddTo(_cts.Token);
            ui.OnInteractStatusUpObject.Subscribe(info => ShowStatusUpUI(info.Item1, info.Item2))
                .AddTo(_cts.Token);
            ui.OnChangeDescriptionUI.Subscribe(ChangeExhibitDescriptionUI).AddTo(_cts.Token);
            ui.OnChangeScoreText.Subscribe(ChangeScore).AddTo(_cts.Token);
            ui.TimeOverlayMessage += TimeOverlayMessage;
            ui.OnOutField.Subscribe(x => _fieldOutUI.alpha = x ? 1f : 0f).AddTo(this);
            ui.OnNotice.Subscribe(x => _noticeView?.ShowNotice(x.Item1, x.Item2)).AddTo(this);
        }
        private void SetupUI()
        {
            if (!_uiRoot)
            {
                _uiRoot = Instantiate(_inGameUiRootPrefab, _mainCanvas.transform);
                //  フェードより後ろに表示するためヒエラルキー一番上に移動
                _uiRoot.transform.SetAsFirstSibling();
            }

            UIController.I.UIRootRefs = _uiRoot;
            _optionUI = _uiRoot.OptionUI;
            _LogPanel = _uiRoot.LogPanel;
            _ogreUiInstance = _uiRoot.OgreUI;
            _changeTagOverlayMessage = _uiRoot.ChangeTagOverlayMessage;
            _timeOverlayMessage = _uiRoot.TimeOverlayMessage;
            _hpBarSlider = _uiRoot.HpBar;
            if (UIController.I.HasHealthRatio)
                _hpBarSlider.Initialize(UIController.I.OnHealthRatioChanged.Value);
            _scoreText = _uiRoot.ScoreText;
            _staminaBarSlider = _uiRoot.StaminaBar;
            _interactUI = _uiRoot.InteractUI;
            _statusUpUI = _uiRoot.StatusUpGroup;
            _statusUpLayout = _uiRoot.StatusUpUIRoot;
            _fieldOutUI = _uiRoot.FieldOutUI;
            _noticeView = _uiRoot.NoticeUI;
            _optionUI.SetActive(true);
            _LogPanel.SetActive(true);
            _ogreUiInstance.SetActive(false);
            _hpBarSlider.gameObject.SetActive(true);
            _staminaBarSlider.gameObject.SetActive(true);
            _interactUI.SetActive(false);
            _statusUpUI.gameObject.SetActive(true);
            _fieldOutUI.gameObject.SetActive(true);
            _fieldOutUI.alpha = 0;
        }

        private void ChangeHp(float healthRatio)
        {
            if (!_hpBarSlider || !UIController.I.HasHealthRatio)
                return;
            _hpBarSlider.SetGauge(healthRatio);
        }

        private void ChangeScore(int value)
        {
            if (!_scoreText) return;

            int.TryParse(_scoreText.text, out int currentValue);

            DOTween.To(() => currentValue, x =>
                {
                    currentValue = Mathf.RoundToInt(x);
                    _scoreText.text = currentValue.ToString();
                }, value, 0.5f)
                .SetEase(Ease.OutCubic)
                .OnComplete(() => _scoreText.text = value.ToString());

            // ポップアニメ
            _scoreText.transform
                .DOScale(1.2f, 0.2f)
                .SetEase(Ease.OutBack)
                .OnComplete(() => _scoreText.transform.DOScale(1f, 0.2f));
        }

        // private async UniTask PlayResultAnimation()
        // {
        //     ResultUIRootRefs resultUI = Instantiate(_resultUIRootPrefab, _mainCanvas.transform);
        //     ResultAnimation resultAnim = resultUI.GetComponent<ResultAnimation>();
        //     await resultAnim.Play(resultUI);
        // }

        private void ChangeExhibitDescriptionUI(ControlDescriptionType type)
        {
            _controlsUIGenerator.GenerateDescription(type);
        }

        private void ChangeStamina(float value)
        {
            if (!_staminaBarSlider)
                return;

            _staminaBarSlider.value = value;
        }

        // キルのログを直接引数に入れる
        // キルのログを直接引数に入れる
        private async UniTask ShowLog(string killText)
        {
            // プレハブから新しいログを作成
            GameObject log = Instantiate(_killLogItemText, _LogPanel.transform);
            TextMeshProUGUI tmp = log.GetComponent<TextMeshProUGUI>();
            tmp.text = killText;

            // フェード用CanvasGroup
            CanvasGroup cg = log.GetComponent<CanvasGroup>() ?? log.AddComponent<CanvasGroup>();
            cg.alpha = 0;

            // 入場アニメーション (フェードイン＋上からスライド)
            log.transform.localScale = Vector3.one * 0.9f;
            await DOTween.Sequence()
                .Append(cg.DOFade(1f, 0.3f))
                .Join(log.transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack));

            // キュー管理
            _killLogQueue.Enqueue(log);
            if (_killLogQueue.Count > _maxLogCount)
            {
                GameObject old = _killLogQueue.Dequeue();
                if (old)
                {
                    CanvasGroup oldCg = old.GetComponent<CanvasGroup>() ?? old.AddComponent<CanvasGroup>();
                    oldCg.DOFade(0f, 0.5f)
                        .OnComplete(() => Destroy(old));
                }
            }

            // 一定時間後に自動で消えるなら追加
            await UniTask.Delay(TimeSpan.FromSeconds(3));
            if (log)
            {
                cg.DOFade(0f, 0.5f).OnComplete(() => Destroy(log));
            }
        }
        private async UniTask ShowGameStartTime(NetworkRunner runner)
        {
            if (!_uiRoot || !_uiRoot.TimerText)
                return;

            TextMeshProUGUI timer = _uiRoot.TimerText;
            timer.gameObject.SetActive(true);

            // Tick基準
            int tickRate = runner.TickRate;

            // カウントダウン
            int preStartEndTick = runner.Tick + _timerData.PreStartTime * tickRate;
            while (runner.Tick < preStartEndTick)
            {
                int remaining = preStartEndTick - runner.Tick;
                timer.text = Mathf.CeilToInt(remaining / (float)tickRate).ToString();
                await UniTask.Yield(PlayerLoopTiming.Update, _cts.Token);
            }

            // ゲーム時間
            int gameEndTick = runner.Tick + (int)(_timerData.GameTime * tickRate);
            int lastTick = runner.Tick;
            while (runner.Tick < gameEndTick)
            {
                if (runner.Tick == lastTick)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update, _cts.Token);
                    continue;
                }

                int remaining = gameEndTick - runner.Tick;
                int seconds = Mathf.CeilToInt(remaining / (float)tickRate);
                timer.text = TimeSpan.FromSeconds(seconds).ToString(@"mm\:ss");
                await UniTask.Yield(PlayerLoopTiming.Update, _cts.Token);
            }

            timer.text = "Time Up!";
            await UniTask.Delay(TimeSpan.FromSeconds(_timerData.Duration), cancellationToken: _cts.Token);
        }
        // 鬼の時にUIを表示する
        private void ShowOgreLamp(bool isShow)
        {
            if (!_ogreUiInstance) return;
            _ogreUiInstance.gameObject.SetActive(isShow);
        }

        private void ShowOptionUI(bool isShow)
        {
            if (_optionUI)
                _optionUI.SetActive(isShow);
        }

        private async UniTaskVoid ShowStatusUpUI(float seconds, StatusUpType statusUpType)
        {
            switch (statusUpType)
            {
                case StatusUpType.Heal:
                    {
                        var ui = Instantiate(_statusUpUI, _statusUpLayout.transform);
                        ui.GetComponentInChildren<TextMeshProUGUI>().text = "バイオリン：体力が回復した";
                        UpdateLayOutGroup();
                        await ui.DOFade(1, 0.5f);
                        await UniTask.Delay(TimeSpan.FromSeconds(seconds));
                        await ui.DOFade(0, 0.5f);
                        Destroy(ui?.gameObject);
                        UpdateLayOutGroup();
                        break;
                    }
                case StatusUpType.Tutankhamen:
                    {
                        var ui = Instantiate(_statusUpUI, _statusUpLayout.transform);
                        ui.GetComponentInChildren<TextMeshProUGUI>().text = "ツタンカーメン：移動速度と攻撃力が上昇中";
                        UpdateLayOutGroup();
                        await ui.DOFade(1, 0.5f);
                        await UniTask.Delay(TimeSpan.FromSeconds(seconds - 1f));
                        await ui.DOFade(0, 0.5f);
                        Destroy(ui?.gameObject);
                        UpdateLayOutGroup();
                        break;
                    }
                case StatusUpType.Ogre:
                    {
                        var ui = Instantiate(_statusUpUI, _statusUpLayout.transform);
                        ui.GetComponentInChildren<TextMeshProUGUI>().text = "鬼：移動速度と攻撃力が上昇中";
                        UpdateLayOutGroup();
                        _ogreGroup = ui;
                        await ui.DOFade(1, 0.5f);
                        break;
                    }
                case StatusUpType.BokeBoke:
                    {
                        var ui = Instantiate(_statusUpUI, _statusUpLayout.transform);
                        ui.GetComponentInChildren<TextMeshProUGUI>().text = "モアイ：必殺技ゲージがたまった";
                        UpdateLayOutGroup();
                        await ui.DOFade(1, 0.5f);
                        await UniTask.Delay(TimeSpan.FromSeconds(seconds - 1f));
                        await ui.DOFade(0, 0.5f);
                        Destroy(ui?.gameObject);
                        UpdateLayOutGroup();
                        break;
                    }
                case StatusUpType.JewelrySpawn:
                    {
                        var ui = Instantiate(_statusUpUI, _statusUpLayout.transform);
                        TextMeshProUGUI text = ui.GetComponentInChildren<TextMeshProUGUI>();
                        text.text = "宝石がスポーンします";
                        text.color = new Color(1f, 0.8f, 0.47f);
                        UpdateLayOutGroup();
                        await ui.DOFade(1, 0.5f);
                        await UniTask.Delay(TimeSpan.FromSeconds(seconds - 1f));
                        await ui.DOFade(0, 0.5f);
                        Destroy(ui?.gameObject);
                        UpdateLayOutGroup();
                        break;
                    }
                case StatusUpType.None:
                    {
                        Destroy(_ogreGroup?.gameObject);
                        UpdateLayOutGroup();
                        break;
                    }
                default:
                    break;
            }


        }

        private void UpdateLayOutGroup()
        {
            // レイアウト内の入力値を再計算
            _statusUpLayout.CalculateLayoutInputHorizontal();

            // レイアウト再設定(表示更新)
            _statusUpLayout.SetLayoutHorizontal();
        }
        /// <summary>
        /// Bind時に_timeOverlayMessageが生成されないのでメソッドを挟む
        /// </summary>
        private UniTask TimeOverlayMessage(TimeMessageType type) => _timeOverlayMessage.CallTask(type);
        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            if (UIController.I) UIController.I.TimeOverlayMessage -= TimeOverlayMessage;
        }
    }
}
