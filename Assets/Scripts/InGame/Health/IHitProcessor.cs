using InGame.Health;

namespace September.InGame.Jewelry
{
    public interface IHitProcessor
    {
        void OnHitTaken(HitData hitData);
    }
}
