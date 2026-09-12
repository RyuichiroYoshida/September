namespace September.InGame.Jewelry.Drop.Strategies.Chances
{
    public interface IJewelryDropChance
    {
        public float GetChance(in JewelryDropContext context);
    }
}
