using System;

namespace September.OgreSystem
{
    public class OgreManager
    {
        private static OgreManager _instance;

        public static OgreManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new OgreManager();
                return _instance;
            }
        }

        /// <summary>
        /// プレイヤーを初期登録する
        /// </summary>
        /// <param name="playerData"></param>
        public void Register(PlayerData playerData)
        {
            PlayerDatabase.Instatnce.Register(playerData);
        }

        /// <summary>
        /// 鬼を抽選するメソッド
        /// </summary>
        public void ChooseOger()
        {
            var allPlayers = PlayerDatabase.Instatnce.GetAll();
            if (allPlayers.Count == 0) return;

            int index = UnityEngine.Random.Range(0, allPlayers.Count);
            var newOni = allPlayers[index];
            
            
            foreach (var player in allPlayers)
            {
                PlayerData current;
                if (player.ID == newOni.ID)
                {
                    current = player.SetOgre(true);
                    //player.GameEventListener.OnBecomeOgre();
                }
                else
                {
                    current = player.SetOgre(false);
                    //player.GameEventListener.OnBecomeNormal();
                }
                
                //データベースへの更新
                PlayerDatabase.Instatnce.Update(current);
            }
        }

        /// <summary>
        /// スタン状態の回復
        /// </summary>
        /// <param name="id"></param>
        public void RecoverStunned(int id)
        {
            PlayerDatabase.Instatnce.TryGetPlayerData(id, out var playerData);
            var current = playerData.SetStunned(false);
            
            //データベースへの更新
            PlayerDatabase.Instatnce.Update(current);
        }

        /// <summary>
        /// HP管理
        /// </summary>
        /// <param name="targetID">ダメージを与えられるID</param>
        /// <param name="attackerID">ダメージを与えるID</param>
        /// <param name="damage">ダメージ</param>
        public void SetHp(int targetID, int attackerID, int damage)
        {
            if (!PlayerDatabase.Instatnce.TryGetPlayerData(targetID, out var target)) return;
            if (!PlayerDatabase.Instatnce.TryGetPlayerData(attackerID, out var attacker)) return;
            
            //ダメージ計算
            int newHp = Math.Max(target.CurrentHp - damage, 0);
            
            //HP変更
            target = target.SetHp(newHp);
            
            if (target.CurrentHp <= 0)
            {
                //気絶処理
                target = target.SetStunned(true);
                //target.GameEventListener.OnParalyzed();

                //鬼の交代処理
                if (attacker.IsOgre)
                {
                    attacker = attacker.SetOgre(false);
                    target = target.SetOgre(true);
                    //attacker.GameEventListener.OnBecomeNormal();
                    //target.GameEventListener.OnBecomeOgre();
                }
            }
            
            //データベースへの更新
            PlayerDatabase.Instatnce.Update(attacker);
            PlayerDatabase.Instatnce.Update(target);
        }
    }
}