using NaughtyAttributes;
using UnityEngine;

namespace September.InGame.UI
{
    public enum UIType
    {
        None,
        SwoonSlider,
        StaminaSlider,
        Button,
        Score,
    }
    
    public class UIAnimation : MonoBehaviour
    {
        [SerializeField,Label("UIのタイプ")] private UIType _uiType = UIType.None;
        [SerializeField,Label("AnimationDataBase")] private UIAnimationDataBase _dataBase;
        private Animator _animator;

        private void Start()
        {
            Initialize();
        }

        private void Initialize()
        {
            _animator = GetComponent<Animator>();

            if (_animator == null)
            {
                Debug.LogWarning("Animator is null");
                return;
            }
            
            var controller = _dataBase.GetClip(_uiType);
            if (controller != null)
            {
                _animator.runtimeAnimatorController = controller;
            }
            else
                Debug.LogWarning("AnimationController is null");
        }

        public void Play(string clipName)
        {
            _animator.Play(clipName);
        }
    }
}