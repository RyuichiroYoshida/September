using Fusion;

namespace September.Common
{
    public class PhotonPlayerRef : IPlayerRef
    {
        public PlayerRef Ref { get; }

        public PhotonPlayerRef(PlayerRef playerRef)
        {
            Ref = playerRef;
        }

        /// <summary>
        /// 型キャスト機能：PlayerRefが必要なときに使う
        /// </summary>
        public T As<T>()
        {
            if (typeof(T) == typeof(PlayerRef))
            {
                return (T)(object)Ref;
            }

            throw new System.InvalidCastException($"Cannot cast PhotonPlayerRef to {typeof(T).Name}");
        }

        public override string ToString()
        {
            return Ref.ToString();
        }

        public override bool Equals(object obj)
        {
            return obj is PhotonPlayerRef other && Ref == other.Ref;
        }

        public override int GetHashCode()
        {
            return Ref.GetHashCode();
        }
    }
}