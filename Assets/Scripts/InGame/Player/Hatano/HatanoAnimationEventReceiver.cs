using UnityEngine;

namespace InGame.Player.Hatano
{
    /// <summary>
    /// ハタノのアニメーションイベントを管理する
    /// </summary>
    public class HatanoAnimationEventReceiver : MonoBehaviour
    {
        [SerializeField] private HatanoAbilityStatusManagement _abilityStatusManagement;
        [SerializeField] private HatanoChangeAnimationController _changeAnimationController;
        
        public void OnChangeAnimationEnd()
        {
            // 切替アニメーションの終了後に構えアニメーションを再生する
            _changeAnimationController.ChangeAimPoseAnimation(_abilityStatusManagement.AbilityStatus);
        }
    }
}
