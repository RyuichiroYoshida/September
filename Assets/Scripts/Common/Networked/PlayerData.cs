using Fusion;

namespace September.Common
{
    public struct PlayerData : INetworkStruct
    {
        public NetworkString<_16> NickName;
        public CharacterType CharacterType;
        public NetworkBool IsOgre;
    }
}