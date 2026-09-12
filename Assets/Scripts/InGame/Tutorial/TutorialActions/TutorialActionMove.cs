using InGame.Player;
using System;
using UnityEngine;

namespace September.InGame.Tutorial
{
    [Serializable]
    public class TutorialActionMove : TutorialActionBase
    {
        
        [Header("ターゲット")]
        [SerializeField] private Transform[] _walkTarget;
        private int _warkTargetIndex = 0;
        [SerializeField] private TutorialMoveGuide _guide;
        [SerializeField] private float _targetRange = 3f;
        [SerializeField] private LayerMask _playerLayer = 1 << 6; // プレイヤーのレイヤー
        private PlayerMovement _playerMovement;
        private const int CHECK_FRAME_INTERVAL = 5;
        private bool _isMoveCompleted = false;
        private bool _isRunCompleted = false;
        private bool _isVaultCompleted = false;

        private enum MoveType
        {
            Walk,
            Run,
            Overcome
        }
        public override void OnStart(TutorialActionData actionData)
        {
            base.OnStart(actionData);
            ConditionTextSet();
            if (actionData.Player.TryGetComponent(out PlayerMovement playerMovement))
            {
                _playerMovement = playerMovement;
            }
            else
            {
                Debug.LogError("PlayerMovementコンポーネントが見つかりません。");
            }
            actionData.TutorialText.text = _explanationText;
            _guide.Show(actionData.Player.transform, _walkTarget[_warkTargetIndex]);
        }

        public override void OnUpdate()
        {
            if (_isCompleted) return;
            base.OnUpdate();

            WalkMove();
            RunMove();
            VaultMove();

            if (_isMoveCompleted && _isRunCompleted && _isVaultCompleted)
            {
                _isCompleted = true;
                _actionData.Action?.Invoke();   
            }
        }

        private void WalkMove()
        {
            if (_isMoveCompleted) return;
            if (IsInPlayerInTarget(_walkTarget, _warkTargetIndex))
            {
                if (!TryToNextTarget(_walkTarget, ref _warkTargetIndex))
                {
                    _isMoveCompleted = true;
                }
            }
        }

        private void RunMove()
        {
            if (_playerMovement.IsDash && !_isRunCompleted)
            {
                _isRunCompleted = true;
                ConditionTextSet();
            }
        }

        private void VaultMove()
        {
            if (_playerMovement.DoingVault && !_isVaultCompleted)
            {
                _isVaultCompleted = true;
                ConditionTextSet();
            }
        }

        /// <summary>
        /// 次のターゲットに移動する処理
        /// </summary>
        private bool TryToNextTarget(Transform[] targets, ref int targetIndex)
        {
            targetIndex++;
            ConditionTextSet();
            // ターゲットのインデックスが範囲外になった場合の処理
            if (targetIndex >= targets.Length)
            {
                // すべてのターゲットをクリアした場合の処理
                Debug.Log("すべてのターゲットをクリアしました！");
                _guide.Hide();
                return false;
            }
            // 次のターゲットに移動
            _guide.Show(_actionData.Player.transform, targets[targetIndex]);

            return true;
        }

        /// <summary>
        /// 指定した範囲内にプレイヤーがいるかどうかをチェックする
        /// </summary>
        private bool IsInPlayerInTarget(Transform[] targets, int targetIndex)
        {
            // 処理の負荷を減らすため、毎フレームチェックするのではなく、5フレームに1回チェックする
            if (Time.frameCount % CHECK_FRAME_INTERVAL != 0) return false;

            if (targetIndex >= targets.Length)
            {
                Debug.LogWarning("ターゲットのインデックスが範囲外です。");
                return false;
            }

            // 指定した範囲内にプレイヤーがいるかどうかをチェック
            Collider[] hitColliders = Physics.OverlapSphere(
                targets[targetIndex].position,
                _targetRange,
                _playerLayer);

            // 範囲内にプレイヤーがいる場合はtrueを返す
            if (hitColliders.Length > 0)
            {
                Debug.Log($"プレイヤーがターゲット{targetIndex}の範囲内にいます。");
                return true;
            }
            return false;
        }

        private void ConditionTextSet()
        {
            string message1 = $"指定場所に移動{_warkTargetIndex}/{_walkTarget.Length}";
            string message2 = $"走る{(_isRunCompleted ? "1" : "0")}/1";
            string message3 = $"柵障害物を乗り越える{(_isVaultCompleted ? "1" : "0")}/1";
            _actionData.ActionConditionText.text = $"{message1}\n{message2}\n{message3}";
        }

        public override void OnEndAction()
        {
            _guide.Hide();
            base.OnEndAction();
            Debug.Log("移動アクション完了");
            _warkTargetIndex = 0;
        }
    }
}
