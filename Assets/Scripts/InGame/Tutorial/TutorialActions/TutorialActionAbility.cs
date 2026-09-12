using InGame.Health;
using InGame.Player.Ability;
using September.InGame.Tutorial;
using UnityEngine;

namespace September
{
    public class TutorialActionAbility : TutorialActionBase
    {
        [SerializeField] private TutorialDummyEnemy _dummyEnemy;
        private PlayerAbilityManager _playerAbilityManager;
        private bool _isNormalAttacked = false;
        private bool _isAbilityAttacked = false;
        public override void OnStart(TutorialActionData actionData)
        {
            base.OnStart(actionData);
            Debug.Log("TutorialActionAbility OnStart");

            if (actionData.Player.TryGetComponent<PlayerAbilityManager>(out var abilityManager))
            {
                _playerAbilityManager = abilityManager;
            }
            else
            {
                Debug.LogError("PlayerAbilityManagerを取得できませんでした。");
            }
            actionData.TutorialText.text = _explanationText;
            ConditionTextSet();
            _dummyEnemy.OnStartAbilityTutorial(OnAttackDummy);
        }

        public void OnAttackDummy(HitData hitData)
        {
            Debug.Log($"TutorialActionAbility OnAttackDummy: {hitData.Executor.ToString()}");
            
            // 通常攻撃、それ以外はアビリティ攻撃と判定
            if (hitData.Executor is AbilityNormalAttack ||
                hitData.Executor is AbilityMultiHitAttack)
            {
                _isNormalAttacked = true;
            }
            else
            {
                _isAbilityAttacked = true;
            }
        }

        public override void OnUpdate()
        {
            if (_isCompleted) return;
            base.OnUpdate();

            if (_isNormalAttacked) ConditionTextSet();
            if (_isAbilityAttacked) ConditionTextSet();

            if (_isNormalAttacked && _isAbilityAttacked)
            {
                _isCompleted = true;
                _actionData.Action?.Invoke();
            }
        }

        private void ConditionTextSet()
        {
            string message1 = $"通常攻撃をあてる{(_isNormalAttacked ? "1" : "0")}/1";
            string message2 = $"アビリティ攻撃をあてる{(_isAbilityAttacked ? "1" : "0")}/1";
            _actionData.ActionConditionText.text = $"{message1}\n{message2}";
        }

        public override void OnEndAction()
        {
            base.OnEndAction();
            _dummyEnemy.OnEndAbilityTutorial();
        }
    }
}
