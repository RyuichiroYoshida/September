using Fusion;

namespace September.Common
{
    public struct SessionPlayerData : INetworkStruct
    {
        public NetworkString<_16> PureNickName => _nickName;

        public string DisplayNickName
        {
            get
            {
                if (_nickNameOrder == 0)
                {
                    return _nickName.Value;
                }
                else
                {
                    return $"{_nickName.Value}_{_nickNameOrder}";
                }
            }
        }
        readonly NetworkString<_16> _nickName;
        readonly int _nickNameOrder;
        public CharacterType CharacterType;
        public NetworkBool IsOgre;
        public int Score;

        public SessionPlayerData(NetworkString<_16> nickName, int nickNameOrder)
        {
            _nickName = nickName;
            _nickNameOrder = nickNameOrder;
            CharacterType = CharacterType.OkabeWright;
            IsOgre = false;
            Score = 0;
        }
    }
}