using System;
using Fusion;
using Fusion.Addons.Physics;
using September.Common;
using UnityEngine;

namespace InGame.Player
{
    public class PlayerMovement : NetworkBehaviour
    {
        [SerializeField] float _moveSpeed = 5f;

        private Rigidbody _rb;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
        }

        public override void FixedUpdateNetwork()
        {
            if (GetInput<PlayerInput>(out var input))
            {
                Move(input.MoveDirection, input.CameraYaw);
            }
        }

        void Move(Vector2 moveInput, float cameraYaw)
        {
            // CameraYawから入力を回転させる
            float radYaw = -cameraYaw * Mathf.Deg2Rad;
            Vector3 rotated = new Vector3(
                moveInput.x * Mathf.Cos(radYaw) - moveInput.y * Mathf.Sin(radYaw),
                0f,
                moveInput.x * Mathf.Sin(radYaw) + moveInput.y * Mathf.Cos(radYaw)
            );
            
            Vector3 velocity = rotated * _moveSpeed;
            velocity.y = _rb.linearVelocity.y;
            _rb.linearVelocity = velocity;
        
            // 移動による回転までとりあえずここに書く
            transform.LookAt(transform.position + rotated, Vector3.up);
            _rb.angularVelocity = Vector3.zero;
        }
    }
}
