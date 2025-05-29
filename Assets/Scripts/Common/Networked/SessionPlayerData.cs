using Fusion;

namespace September.Common
{
    public struct SessionPlayerData : INetworkStruct
    {
        public NetworkString<_16> NickName;
        public CharacterType CharacterType;
        public NetworkBool IsOgre;
        public int Score;
    }
}