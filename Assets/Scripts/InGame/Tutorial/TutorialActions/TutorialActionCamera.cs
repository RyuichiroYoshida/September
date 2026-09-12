using September.Common;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace September.InGame.Tutorial
{
    public class TutorialActionCamera : TutorialActionBase
    {
        [Header("カメラをリセットする回数"),SerializeField] private int _resetCount;
        private int _currentResetCount;
        public override void OnStart(TutorialActionData actionData) 
        {
            actionData.TutorialText.text = _explanationText;
            base.OnStart(actionData);
            _currentResetCount = 0;
            ConditionTextSet();
            Debug.Log($"カメラを{_resetCount}回リセットしよう");
        }
        public override void OnUpdate()
        {
            base.OnUpdate();
            if (!_isActionStarted) return;

            // プレイヤーがカメラをリセットしたかチェック
            if (GameInput.I.Player.Aim.triggered)
            {
                _currentResetCount++;
                ConditionTextSet();

                if (_currentResetCount >= _resetCount)
                {
                    _actionData.Action?.Invoke();
                }
            }
        }

        /// <summary>
        /// 条件表示を更新
        /// </summary>
        private void ConditionTextSet()
        {
            _actionData.ActionConditionText.text = 
                $"カメラのリセット{_currentResetCount}/{_resetCount}";
        }

        public override void OnEndAction() 
        {
            base.OnEndAction();
            Debug.Log("カメラリセットアクション完了");
        }
    }
}
