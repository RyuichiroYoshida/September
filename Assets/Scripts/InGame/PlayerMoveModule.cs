using September.Common;
using UnityEngine;

namespace September.InGame
{
    public class PlayerMoveModule : BasePlayerModule
    {
        public override void FixedUpdateNetwork()
        {
            if (!GetInput<MyInput>(out var input)) return;
            var velocity = Rigidbody.linearVelocity;
            var dir = new Vector3(input.MoveDirection.x, 0f, input.MoveDirection.y) * PlayerData.Speed;
            if (dir != Vector3.zero)
            {
                transform.forward = dir;
            }
            velocity.x = dir.x;
            velocity.z = dir.z;
            Rigidbody.linearVelocity = velocity;
        }
    }
}