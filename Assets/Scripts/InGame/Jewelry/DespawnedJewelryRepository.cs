using System.Collections.Generic;
using InGame.Jewelry.Common;

namespace September.InGame.Jewelry
{
    public static class DespawnedJewelryRepository
    {
        private static readonly Dictionary<JewelryType, int> DespawnedJewelryCount = new();

        public static void AddDespawnedJewelry(JewelryType jewelryType)
        {
            if (!DespawnedJewelryCount.TryAdd(jewelryType, 1))
            {
                DespawnedJewelryCount[jewelryType]++;
            }
        }

        public static int GetDespawnedJewelryCount(JewelryType jewelryType)
        {
            return DespawnedJewelryCount.GetValueOrDefault(jewelryType, 0);
        }

        public static IEnumerable<KeyValuePair<JewelryType, int>> GetDespawnedJewelryCount() => DespawnedJewelryCount;

        public static void Clear() => DespawnedJewelryCount.Clear();
    }
}
