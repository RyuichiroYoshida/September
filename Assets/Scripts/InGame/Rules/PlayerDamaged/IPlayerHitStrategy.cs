using InGame.Health;

namespace September.InGame.Rules.PlayerDamaged
{
    public interface IPlayerHitStrategy
    {
        public void OnHitTaken(ref HitData hitData);
    }
}
