using Fusion;

namespace InGame.Health
{
    /// <summary> Hit情報 </summary>
    public struct HitData
    {
        public HitActionType HitActionType;
        public int Amount;
        public bool IsLastHit;
        public PlayerRef ExecutorRef;
        public IHitExecutor Executor;
        public PlayerRef TargetRef;
        public IDamageable Target;

        public HitData(HitActionType actionType, int amount, PlayerRef executorRef, PlayerRef targetRef, IHitExecutor executor = null, IDamageable target = null)
        {
            HitActionType = actionType;
            Amount = amount;
            IsLastHit = false;
            ExecutorRef = executorRef;
            Executor = executor;
            TargetRef = targetRef;
            Target = target;
        }

        public override string ToString()
        {
            return $"ActionType: {HitActionType}\n" +
                   $"Amount:     {Amount}\n" +
                   $"IsLastHit:  {IsLastHit}\n" +
                   $"Executor:   {ExecutorRef}\n" +
                   $"Target:     {TargetRef}";
        }
    }

    public enum HitActionType
    {
        Damage,
        Heal
    }
    
    public interface IDamageable
    {
        bool IsAlive { get; }
        PlayerRef OwnerPlayerRef { get; }
        void TakeHit(ref HitData hitData);
    }

    /// <summary> Hitを与えるもの </summary>
    public interface IHitExecutor
    {
        void HitExecution(HitData hitData);
    }

    public static class HitUtility
    {
        public static HitData ApplyHit(ref HitData hitData)
        {
            hitData.Target.TakeHit(ref hitData);
            
            return hitData;
        }
    }
}
