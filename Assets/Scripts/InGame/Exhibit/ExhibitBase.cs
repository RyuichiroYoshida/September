using Fusion;
using UnityEngine;

namespace September.InGame
{
    public abstract class ExhibitBase : MonoBehaviour
    {
        [SerializeField] private string _exhibitName;
        [SerializeField] private Renderer _renderer;
        
        # region モックアップのみ
        
        private BaseAbility _currentBaseAbility;
        private Color _playerColor;
        private bool _isPlayerInRange = false;
        
        // private void OnTriggerEnter(Collider other)
        // {
        //     if (other.TryGetComponent<PlayerAbilityController>(out var playerInfo))
        //     {
        //         _currentBaseAbility = playerInfo.Ability;
        //
        //         HighLight(_playerColor);
        //         _isPlayerInRange = true;
        //     }
        // }
        //
        // private void OnTriggerExit(Collider other)
        // {
        //     if (other.TryGetComponent<PlayerAbilityController>(out var playerInfo))
        //     {
        //         UnHighlight();
        //         _currentBaseAbility = null;
        //         _isPlayerInRange = false;
        //     }
        // }
        //
        // private void Update()
        // {
        //     if (_isPlayerInRange && Input.GetKeyDown(KeyCode.E))
        //     {
        //         if (_currentBaseAbility != null)
        //         {
        //             Interact(_currentBaseAbility);
        //         }
        //     }
        // }
        
        #endregion
        
        /// <summary>固有アビリティの設定</summary>
        public abstract void Interact(BaseAbility baseAbility);

        public virtual void HighLight(Color color)
        {
            if(_renderer != null)
                _renderer.material.color = color;
        }

        public virtual void UnHighlight()
        {
            if(_renderer != null)
                _renderer.material.color = Color.white;
        }
    }
}