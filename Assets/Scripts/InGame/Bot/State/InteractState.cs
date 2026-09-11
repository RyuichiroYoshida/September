using InGame.Interact;
using September.Common;
using UnityEngine;

namespace InGame.Bot
{
    public class InteractState : IBotState
    {
        private InteractableBase _targetInteractable;

        public void OnEnter(BotStateMachine stateMachine)
        {
            _targetInteractable = BotDataBase.Instance.GetNearbyInteractable(stateMachine.gameObject);
        }

        public void OnExit(BotStateMachine stateMachine)
        {

        }

        public void OnUpdate(BotStateMachine stateMachine)
        {
            if (_targetInteractable == null)
            {
                OnEnter(stateMachine);
                return;
            }

            Vector3 targetPosition = _targetInteractable.GetNearestPointOnInteractArea(stateMachine.transform.position);
            stateMachine.Navigation.GetDestinationInput(stateMachine.transform.position, targetPosition);

            //ターゲットがクールダウンになったら終了
            if (_targetInteractable.IsInCooldown())
            {
                stateMachine.ChangeState();
            }
        }

        public bool? GetInputButton(BotStateMachine stateMachine, PlayerButtons button)
        {
            switch (button)
            {
                case PlayerButtons.Interact:
                    return true;
                case PlayerButtons.Jump:
                    return stateMachine.Navigation.IsVaultInput;
            }

            return null;
        }

        public Vector2 GetInputDirection(BotStateMachine stateMachine)
        {
            return stateMachine.Navigation.InputDirection;
        }
    }
}
