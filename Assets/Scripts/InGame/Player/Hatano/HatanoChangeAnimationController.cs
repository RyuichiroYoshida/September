using Fusion;
using InGame.Common;
using UnityEngine;

namespace InGame.Player.Hatano
{
    /// <summary>
    /// 装備しているアビリティに応じて移動アニメーションを変更する
    /// </summary>
    public class HatanoChangeAnimationController : NetworkBehaviour
    {
        [SerializeField] private AnimationClipPlayer _animationClipPlayer;
        [Header("二丁拳銃（Aim）"), SerializeField] private AnimationClip _doubleAimPoseClip;
        [Header("レーザー銃（Aim）"), SerializeField] private AnimationClip _laserAimPoseClip;
        [Header("二丁拳銃（移動）")]
        [SerializeField] private AnimationClip _doubleWaitClip;
        [SerializeField] private AnimationClip _doubleWalkClip;
        [SerializeField] private AnimationClip _doubleRunClip;
        [Header("レーザー銃（移動）")]
        [SerializeField] private AnimationClip _laserWaitClip;
        [SerializeField] private AnimationClip _laserWalkClip;
        [SerializeField] private AnimationClip _laserRunClip;

        /// <summary>
        /// アビリティに応じて移動アニメーションを変更
        /// </summary>
        /// <param name="status">変更後のアビリティ</param>
        public void ChangeMoveAnimation(HatanoAbilityStatus status)
        {
            switch (status)
            {
                case HatanoAbilityStatus.DoubleBarreledGun:
                    _animationClipPlayer.ChangeWaitAnimationClip(_doubleWaitClip);
                    _animationClipPlayer.ChangeWalkAnimationClip(_doubleWalkClip);
                    _animationClipPlayer.ChangeRunAnimationClip(_doubleRunClip);
                    break;
                
                case HatanoAbilityStatus.LaserGun:
                    _animationClipPlayer.ChangeWaitAnimationClip(_laserWaitClip);
                    _animationClipPlayer.ChangeWalkAnimationClip(_laserWalkClip);
                    _animationClipPlayer.ChangeRunAnimationClip(_laserRunClip);
                    break;
            }
        }

        /// <summary>
        /// アビリティに応じて構えアニメーションを再生する
        /// </summary>
        /// <param name="status">変更後のアビリティ</param>
        public void ChangeAimPoseAnimation(HatanoAbilityStatus status)
        {
            switch (status)
            {
                case HatanoAbilityStatus.DoubleBarreledGun:
                    _animationClipPlayer.PlayOnUpperBody(_doubleAimPoseClip);
                    break;
                
                case HatanoAbilityStatus.LaserGun:
                    _animationClipPlayer.PlayOnUpperBody(_laserAimPoseClip);
                    break;
            }
        }
    }
}
