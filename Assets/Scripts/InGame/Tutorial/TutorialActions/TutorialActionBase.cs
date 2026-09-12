using System;
using UnityEngine;
using UnityEngine.UI;
using InGame.Player;
using September.Common;
using TMPro;

namespace September.InGame.Tutorial
{
    /// <summary>
    /// チュートリアルで使用する行動のデータ
    /// </summary>
    public struct TutorialActionData
    {
        public Action Action;
        public GameObject Player;
        public GameObject TutorialUI;
        public TextMeshProUGUI TutorialText;
        public Button CloseButton;
        public TextMeshProUGUI ActionConditionText;
        public PlayerInputManager PlayerInputManager;
    }
    /// <summary>チュートリアルで使用する行動</summary>
    [Serializable]
    public class TutorialActionBase
    {
        [SerializeField] protected string _explanationText;
        [SerializeField] protected Sprite _explanationPicture;
        protected bool _isCompleted = false;
        protected TutorialActionData _actionData;
        protected bool _isActionStarted = false;
        public virtual void OnStart(TutorialActionData actionData)
        {
            _actionData = actionData;
            actionData.TutorialUI.SetActive(true);
            actionData.TutorialUI.GetComponent<Image>().sprite = _explanationPicture;
            _isActionStarted = true;
            GameInput.I.IsInputBlockedByUI = true;
            actionData.CloseButton.onClick.AddListener(OnCloseButtonClicked);
            // パネルが既に有効でも、各説明の開始時に決定入力の対象を明示する。
            actionData.CloseButton.Select();
        }

        public virtual void OnUpdate()
        {
            if (!_isActionStarted) return;
        }

        public virtual void OnCloseButtonClicked()
        {
            GameInput.I.IsInputBlockedByUI = false;
        }

        public virtual void OnEndAction() 
        {
            _actionData.CloseButton.onClick.RemoveListener(OnCloseButtonClicked);
        }
    }
}
