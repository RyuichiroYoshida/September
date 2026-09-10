using CriWare;
using Fusion;
using InGame.Health;
using UnityEngine;

namespace Ingame.Tanihira
{
    public class FriendAttackState : IFriendState
    {
        private Transform _friendObject;
        private MeleeHitboxExecutor _executor;
        
        public void OnEnter(FriendBase friend)
        {
            //navmeshでの処理
            if (friend.Agent != null && friend.Agent.enabled && friend.Agent.isOnNavMesh)
            {
                friend.Agent.isStopped = true;
                _friendObject = friend.Agent.gameObject.transform;
                LookTarget(friend.Agent.destination);
                friend.Agent.SetDestination(friend.Agent.destination);
            }
            friend.MecanimAnimator?.SetTrigger("Attack"); // アニメーターにAttackトリガーがある前提
        }

        public void OnExit(FriendBase friend)
        {
            if (friend.Agent != null && friend.Agent.enabled && friend.Agent.isOnNavMesh)
            {
                //Navmeshを再開
                friend.Agent.isStopped = false;
            }
        }

        public void OnUpdate(FriendBase friend)
        {
            
        }

        private void LookTarget(Vector3 target)
        {
            Vector3 direction = target - _friendObject.position;
            direction.y = 0f; // 上下方向は無視

            if (direction.sqrMagnitude > 0.01f)
            {
                _friendObject.rotation = Quaternion.LookRotation(direction);
            }
        }
    }
}
