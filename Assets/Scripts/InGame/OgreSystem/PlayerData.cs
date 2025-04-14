namespace September.OgreSystem
{
    public struct PlayerData
    {
        public string ID; //ID（これがキーになる）
        public string PlayerName; //プレイヤー名
        public int MaxHp; //最大HP
        public int CurrentHp; //現在のHP
        public bool IsOgre; //鬼のフラグ
        public bool IsStunned; //スタンのフラグ
        public IGameEventListener GameEventListener; //通知用のインターフェース

        public PlayerData(string id, string playerName, int maxHp, int currentHp, bool isOgre, bool isStunned, IGameEventListener gameEventListener)
        {
            ID = id;
            PlayerName = playerName;
            MaxHp = maxHp;
            CurrentHp = currentHp;
            IsOgre = isOgre;
            IsStunned = isStunned;
            GameEventListener = gameEventListener;
        }
        
        //状態更新メソッド
        
        /// <summary>
        /// 鬼フラグの更新
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public PlayerData SetOgre(bool value) =>
            new PlayerData(ID, PlayerName, MaxHp, CurrentHp, value, IsStunned, GameEventListener);
        
        /// <summary>
        /// スタンフラグの更新
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public PlayerData SetStunned(bool value) =>
            new PlayerData(ID, PlayerName, MaxHp, CurrentHp, IsOgre, value, GameEventListener);
        
        /// <summary>
        /// 体力の更新
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public PlayerData SetHp(int value) =>
            new PlayerData(ID, PlayerName, MaxHp, value, IsOgre, IsStunned, GameEventListener);
    }
}

 