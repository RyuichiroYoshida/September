using System;
using Fusion;

namespace September.InGame.Effect
{
    public readonly struct EffectID : INetworkStruct, IEquatable<EffectID>
    {
        private readonly int id;
        private readonly PlayerRef spawnInvokerRef;

        public bool IsValid => id > 0;

        public EffectID(int id, PlayerRef spawnInvokerRef)
        {
            this.id = id;
            this.spawnInvokerRef = spawnInvokerRef;
        }

        public bool Equals(EffectID other)
        {
            return id == other.id && spawnInvokerRef == other.spawnInvokerRef;
        }

        public override bool Equals(object obj)
        {
            return obj is EffectID other && Equals(other);
        }

        public override int GetHashCode()
        {
            return id.GetHashCode() + spawnInvokerRef.GetHashCode();
        }

        public override string ToString()
        {
            return $"{id}_{spawnInvokerRef.PlayerId}";
        }
    }
}
