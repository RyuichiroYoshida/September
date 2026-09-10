using Fusion;
using UnityEngine;

namespace September.Common
{
    public static class LocalPlayer
    {
        public static PlayerRef PlayerRef
        {
            get
            {
                try
                {
                    return NetworkRunner.Instances[0].LocalPlayer;
                }
                catch
                {
                    Debug.LogWarning("[LocalPlayer] NetworkRunner is null");
                    return PlayerRef.None;
                }
            }
        }

        public static CharacterType CharacterType
        {
            get {
                try
                {
                    return PlayerDatabase.Instance.PlayerDataDic[PlayerRef].CharacterType;
                }
                catch
                {
                    Debug.LogWarning("[LocalPlayer] LocalPlayer is not in PlayerDatabase");
                    return CharacterType.None;
                }
            }
        }
    }
}
