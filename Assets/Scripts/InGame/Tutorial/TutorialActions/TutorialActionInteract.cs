using InGame.Interact;
using UnityEngine;

namespace September.InGame.Tutorial
{
    public class TutorialActionInteract : TutorialActionBase
    {
        private PlayerInteractionController _intaractionController;
        private bool _isInteracted = false;
        public override void OnStart(TutorialActionData actionData)
        {
            base.OnStart(actionData);
            ConditionTextSet();
            actionData.TutorialText.text = _explanationText;
            _intaractionController = actionData.Player.GetComponent<PlayerInteractionController>();
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
            if (_intaractionController.IsExecutingInteraction)
            {
                _isInteracted = true;
                ConditionTextSet();
                _actionData.Action?.Invoke();
            }
        }

        private void ConditionTextSet()
        {
            _actionData.ActionConditionText.text = $"インタラクトしよう{(_isInteracted ? "1" : "1")}/1";
        }

        public override void OnEndAction()
        {
            base.OnEndAction();
        }
    }
}
