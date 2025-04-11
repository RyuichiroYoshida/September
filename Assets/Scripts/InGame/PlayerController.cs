using Fusion;
using September.Common;
using UnityEngine;

namespace September.InGame
{
    public class PlayerController : NetworkBehaviour
    {
        Rigidbody _rigidbody;
        [Networked] public NetworkButtons ButtonsPrevious { get; set; }
        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
        }

        public override void FixedUpdateNetwork()
        {
            if (!GetInput<MyInput>(out var input)) return;
            var pressed = input.Buttons.GetPressed(ButtonsPrevious);
            ButtonsPrevious = input.Buttons;
            var velocity = _rigidbody.linearVelocity;
            if (pressed.IsSet(MyButtons.Jump))
            {
                velocity.y = 5f;
                Debug.Log("Jump");
            }
            velocity.x = input.MoveDirection.x * 2f;
            velocity.z = input.MoveDirection.y * 2f;
            _rigidbody.linearVelocity = velocity;
            //  移動方向に体を向ける
            velocity.y = 0f;
            if (velocity != Vector3.zero)
            {
                gameObject.transform.forward = velocity;
            }
        }
    }
}