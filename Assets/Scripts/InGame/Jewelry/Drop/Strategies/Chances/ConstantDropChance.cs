using System;
using UnityEngine;

namespace September.InGame.Jewelry.Drop.Strategies.Chances
{
    [Serializable]
    public class ConstantDropChance : IJewelryDropChance
    {
        [SerializeField] private float _dropChance = 1f;

        public float GetChance(in JewelryDropContext context)
        {
            return _dropChance;
        }
    }
}
