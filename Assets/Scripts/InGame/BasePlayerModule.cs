using Fusion;
using UnityEngine;

namespace September.InGame
{
    public abstract class BasePlayerModule : NetworkBehaviour
    {
        // protected PlayerController PlayerController;
        // protected PlayerCameraController PlayerCameraController;
        // protected PlayerData PlayerData;
        protected Rigidbody Rigidbody;
        protected Animator Animator;

        // public void Initialize(PlayerController playerController, PlayerCameraController cameraController, PlayerData playerData, Rigidbody rb, Animator animator)
        // {
        //     PlayerController = playerController;
        //     PlayerCameraController = cameraController;
        //     PlayerData = playerData;
        //     Rigidbody = rb;
        //     Animator = animator;
        // }
        
        public void Initialize(Rigidbody rb, Animator animator)
        {
            Rigidbody = rb;
            Animator = animator;
        }
    }
}