using Fusion;
using September.Common;
using UnityEngine;

namespace September.InGame
{
    public class PlayerController : NetworkBehaviour
    {
        [SerializeField] float _moveSpeed = 5f;
        [SerializeField] Transform _body;
        [SerializeField] Rigidbody _rigidbody;
        //[Networked] public NetworkButtons ButtonsPrevious { get; set; }
        public override void FixedUpdateNetwork()
        {
            if (!GetInput<MyInput>(out var input)) return;
            // var pressed = input.Buttons.GetPressed(ButtonsPrevious);
            // ButtonsPrevious = input.Buttons;
            var velocity = _rigidbody.linearVelocity;
            // if (pressed.IsSet(MyButtons.Jump))
            // {
            //     velocity.y = 5f;
            // }

            var dir = new Vector3(input.MoveDirection.x, 0f, input.MoveDirection.y) * _moveSpeed;
            if (dir != Vector3.zero)
            {
                transform.forward = dir;
            }
            velocity.x = dir.x;
            velocity.z = dir.z;
            _rigidbody.linearVelocity = velocity;
        }
    }
}