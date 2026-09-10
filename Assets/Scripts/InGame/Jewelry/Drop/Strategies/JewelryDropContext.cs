using InGame.Health;
using InGame.Jewelry.Common;

namespace September.InGame.Jewelry.Drop.Strategies
{
    public struct JewelryDropContext
    {
        public HitData HitData;
        public JewelryType JewelryType;
        public IJewelryContainer VictimJewelryContainer;
        public int Amount;

        public JewelryDropContext(HitData hitData, JewelryType jewelryType, IJewelryContainer victimJewelryContainer)
        {
            HitData = hitData;
            JewelryType = jewelryType;
            VictimJewelryContainer = victimJewelryContainer;
            Amount = 0;
        }
    }
}
