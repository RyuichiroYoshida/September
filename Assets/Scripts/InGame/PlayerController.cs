using Fusion;
using September.Common;
using UnityEngine;

namespace September.InGame
{
    public class PlayerController : NetworkBehaviour
    {
        Rigidbody _rigidbody;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
        }

        public override void FixedUpdateNetwork()
        {
            if (!GetInput<MyInput>(out var input)) return;
            Debug.Log(input.MoveDirection);
            var velocity = _rigidbody.linearVelocity;
            velocity.x = input.MoveDirection.x;
            velocity.z = input.MoveDirection.y;
            _rigidbody.linearVelocity = velocity;
        }
    }
}