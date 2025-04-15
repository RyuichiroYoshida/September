using Fusion;

namespace September.OgreSystem
{
    public struct PlayerData
    {
        public int ID; //ID（これがキーになる）
        public NetworkString<_16> PlayerName; //プレイヤー名
        public int MaxHp; //最大HP
        public int CurrentHp; //現在のHP
        public NetworkBool IsOgre; //鬼のフラグ
        public NetworkBool IsStunned; //スタンのフラグ
        //public IGameEventListener GameEventListener; //通知用のインターフェース

        public PlayerData(int id, NetworkString<_16> playerName, int maxHp, int currentHp, bool isOgre, bool isStunned)
        {
            ID = id;
            PlayerName = playerName;
            MaxHp = maxHp;
            CurrentHp = currentHp;
            IsOgre = isOgre;
            IsStunned = isStunned;
            //GameEventListener = gameEventListener;
        }
        
        //状態更新メソッド
        
        /// <summary>
        /// 鬼フラグの更新
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public PlayerData SetOgre(NetworkBool value) =>
            new PlayerData(ID, PlayerName, MaxHp, CurrentHp, value, IsStunned);
        
        /// <summary>
        /// スタンフラグの更新
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public PlayerData SetStunned(NetworkBool value) =>
            new PlayerData(ID, PlayerName, MaxHp, CurrentHp, IsOgre, value);
        
        /// <summary>
        /// 体力の更新
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public PlayerData SetHp(int value) =>
            new PlayerData(ID, PlayerName, MaxHp, value, IsOgre, IsStunned);
    }
}

 