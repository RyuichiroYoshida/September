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
    }

    public enum HitActionType
    {
        Damage,
        Heal
    }
    
    public interface IDamageable
    {
        public bool IsAlive { get; }
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
