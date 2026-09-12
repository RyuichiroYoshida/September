using Cysharp.Threading.Tasks;
using Fusion;
using InGame.Player;
using September.Common;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace September.InGame.Tutorial
{
    public class TutorialManager : NetworkBehaviour
    {
        [Header("チュートリアルのアクションリスト")]
        [SerializeReference, SubclassSelector] private List<TutorialActionBase> _tutorialActions;
        [Header("説明画像を表示するImage")]
        [SerializeField] private GameObject _tutorialUI;
        [Header("説明文を表示するText")]
        [SerializeField] private TextMeshProUGUI _tutorialText;
        [Header("説明UIを非表示にするボタン")]
        [SerializeField] private Button _closeButton;
        [Header("アクションを完了する条件表示")]
        [SerializeField] private TextMeshProUGUI _actionConditionText;
        private int _currentActionIndex = 0;
        [Header("次のアクションに移るまでの待機時間（秒）" +
            "小数第一位まで")]
        [SerializeField] private float _waitTime = 0;
        [SerializeField] private GameObject _endPanel;

        private bool _isWaitingForNextAction = false;
        private bool _isTutorialCompleted = false;
        private bool _hasStarted;
        private TutorialActionData _actionData;

        private void OnValidate()
        {
            // _waitTimeを小数点第一位までの値に丸める
            _waitTime = Mathf.Round(_waitTime * 10f) / 10f;
        }

        public override void Spawned()
        {
            base.Spawned();
        }

        private void Awake()
        {
            _actionData = new TutorialActionData
            {
                Action = OnCompleteCurrentAction,
                TutorialUI = _tutorialUI,
                TutorialText = _tutorialText,
                CloseButton = _closeButton,
                ActionConditionText = _actionConditionText
            };
        }

        /// <summary>
        /// チュートリアルを開始するメソッド
        /// 最初のアクションを開始し、以降は各アクションの完了コールバックで次のアクションを開始する
        /// </summary>
        public void OnTutorialStart(NetworkObject player, PlayerInputManager playerInputManager)
        {
            if (_hasStarted) return;
            if (_tutorialActions == null || _tutorialActions.Count == 0)
                throw new InvalidOperationException("チュートリアルのアクションが設定されていません。");
            Debug.Log($"TutorialManager: OnTutorialStart called for player {player.name}");
            _actionData.Player = player.gameObject;
            _actionData.PlayerInputManager = playerInputManager;
            _tutorialActions[_currentActionIndex].OnStart(_actionData);
            _hasStarted = true;
        }

        private void Update()
        {
            // プレイヤーと各アクションの初期化が終わるまで進行処理を呼ばない。
            if (!_hasStarted || _isTutorialCompleted || _isWaitingForNextAction) return;
            _tutorialActions[_currentActionIndex].OnUpdate();
        }

        private void OnCompleteCurrentAction()
        {
            if (!_hasStarted || _isWaitingForNextAction || _isTutorialCompleted) return;
            _isWaitingForNextAction = true;
            CompleteCurrentActionAsync().Forget();
        }

        private async UniTask CompleteCurrentActionAsync()
        {
            // 現在のアクションを終了する
            _tutorialActions[_currentActionIndex].OnEndAction();

            _currentActionIndex++;
            _isWaitingForNextAction = true;
            // 次のアクションまで待つ
            // シーンを離れた場合は次の説明を開かない。
            if (await UniTask.Delay(TimeSpan.FromSeconds(_waitTime),
                    cancellationToken: this.GetCancellationTokenOnDestroy()).SuppressCancellationThrow()) return;

            // 次のアクションがあれば開始する
            if (_currentActionIndex < _tutorialActions.Count)
            {
                _tutorialActions[_currentActionIndex].OnStart(_actionData);
                _isWaitingForNextAction = false;
            }
            else
            {
                Debug.Log("チュートリアルが完了しました！");
                _isTutorialCompleted = true;
                _endPanel.SetActive(true);
                GameInput.I.IsInputBlockedByUI = true;
                CursorStateManager.ShowCursor();
            }
        }

        private void OnDestroy()
        {
            if (_hasStarted && !_isWaitingForNextAction && !_isTutorialCompleted)
                _tutorialActions[_currentActionIndex].OnEndAction();
        }
    }
}
