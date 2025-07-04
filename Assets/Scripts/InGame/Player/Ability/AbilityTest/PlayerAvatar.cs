using System.Collections;
using System.Collections.Generic;
using Fusion;
using September.Common;
using UnityEngine;

public class PlayerAvatar : NetworkBehaviour
{
    [Networked]
    public NetworkString<_16> NickName { get; set; }
    private NetworkCharacterController characterController;
    private NetworkMecanimAnimator networkMecanimAnimator;
    
    public PlayerRef PlayerRef { get; private set; }

    public override void Spawned()
    {
        characterController = GetComponent<NetworkCharacterController>();
        var networkObject = GetComponent<NetworkObject>();
        networkMecanimAnimator = GetComponentInChildren<NetworkMecanimAnimator>();
        var view = GetComponent<PlayerAvatorView>();
        view.SetNickName(Runner, NickName.Value);
        PlayerRef = networkObject.InputAuthority;

        if (HasInputAuthority)
        {
            view.MakeCameraTarget();
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!GetInput<PlayerInput>(out var input)) return;
        var cameraRotation = Quaternion.Euler(0f, input.CameraYaw, 0f);
       
        var inputDirection = new Vector3( input.MoveDirection.x, 0f, input.MoveDirection.y);
        characterController.Move(cameraRotation * inputDirection);
        // ジャンプ
        if (input.Buttons.IsSet(PlayerButtons.Jump))
        {
            characterController.Jump();
        }
        
        var animator = networkMecanimAnimator.Animator;
        var grounded = characterController.Grounded;
        var vy = characterController.Velocity.y;
        animator.SetFloat("Speed", characterController.Velocity.magnitude);
        animator.SetBool("Jump", !grounded && vy > 4f);
        animator.SetBool("Grounded", grounded);
        animator.SetBool("FreeFall", !grounded && vy < -4f);
        animator.SetFloat("MotionSpeed", 1f);
    }
}
