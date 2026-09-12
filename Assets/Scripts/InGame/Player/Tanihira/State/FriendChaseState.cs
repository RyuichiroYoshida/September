using Ingame.Tanihira;

public class FriendChaseState : IFriendState
{
    public void OnEnter(FriendBase friend)
    {
        friend.Agent.speed = friend.CurrentFriendStatus.FriendChaseSpeed;
        friend.Agent.stoppingDistance = friend.CurrentFriendStatus.FriendChaseStoppingDistance;
    }

    public void OnExit(FriendBase friend)
    {
        
    }

    public void OnUpdate(FriendBase friend)
    {
        if (friend.Destination == null || !friend.Agent.isOnNavMesh) return;
        if (!friend.IsAttackPossible) return;
            
        friend.Agent.SetDestination(friend.Destination.position);
        //速度に応じて、アニメーションを変化させる
        friend.Animator.SetFloat("MoveBlend", friend.Agent.velocity.magnitude);
        friend.ChangeRunEffect(friend.Agent.velocity.magnitude);
        
        if (friend.Agent.remainingDistance <= friend.Agent.stoppingDistance + 0.5f && !friend.IsAttack)
        {
            if(!friend.IsCanAttack) return;
            //エフェクトを消す
            friend.ChangeRunEffect(0.0f);
            friend.ChangeState(FriendState.Attack);
        }
    }
}
