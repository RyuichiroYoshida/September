using System;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

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
            PlayerDatabase.Instance.Register(playerData);
        }

        /// <summary>
        /// 鬼を抽選するメソッド
        /// </summary>
        public void ChooseOgre()
        {
            var allPlayers = PlayerDatabase.Instance.GetAll();
            if (allPlayers.Count == 0) return;

            int index = UnityEngine.Random.Range(0, allPlayers.Count);
            var newOni = allPlayers[index];
            
            
            foreach (var player in allPlayers)
            {
                PlayerData current;
                if (player.ID == newOni.ID)
                {
                    current = player.SetOgre(true);
                    var runner = NetworkRunner.GetRunnerForScene(SceneManager.GetActiveScene());
                    var networkObj = runner.GetPlayerObject(player.PlayerRef);
                    var gameEventListener = networkObj.GetComponent<IGameEventListener>();
                    gameEventListener?.OnBecomeOgre();
                    Debug.Log(player);
                }
                else
                {
                    current = player.SetOgre(false);
                    var runner = NetworkRunner.GetRunnerForScene(SceneManager.GetActiveScene());
                    var networkObj = runner.GetPlayerObject(player.PlayerRef);
                    var gameEventListener = networkObj.GetComponent<IGameEventListener>();
                    gameEventListener?.OnBecomeNormal();
                }
                
                //データベースへの更新
                PlayerDatabase.Instance.UpdateDatabase(current);
            }
        }

        /// <summary>
        /// スタン状態の回復
        /// </summary>
        /// <param name="id"></param>
        public void RecoverStunned(int id)
        {
            PlayerDatabase.Instance.TryGetPlayerData(id, out var playerData);
            var stunnedData = playerData.SetStunned(false);
            
            //HP回復処理
            var current = playerData.SetHp(stunnedData.MaxHp);
            //データベースへの更新
            PlayerDatabase.Instance.UpdateDatabase(current);
        }

        /// <summary>
        /// HP管理
        /// </summary>
        /// <param name="targetID">ダメージを与えられるID</param>
        /// <param name="attackerID">ダメージを与えるID</param>
        /// <param name="damage">ダメージ</param>
        public void SetHp(int targetID, int attackerID, int damage)
        {
             if (!PlayerDatabase.Instance.TryGetPlayerData(targetID, out var target)) return;
             if (!PlayerDatabase.Instance.TryGetPlayerData(attackerID, out var attacker)) return;
            
             //ダメージ計算
             int newHp = Math.Max(target.CurrentHp - damage, 0);
            
             //HP変更
             target = target.SetHp(newHp);
            
             if (target.CurrentHp <= 0)
             {
                 //気絶処理
                 target = target.SetStunned(true);
                 var runner = NetworkRunner.GetRunnerForScene(SceneManager.GetActiveScene());
                 var targetNetworkObj = runner.GetPlayerObject(target.PlayerRef);
                 var targetGameEventListener = targetNetworkObj.GetComponent<IGameEventListener>();

                 targetGameEventListener?.OnParalyzed();

                 //鬼の交代処理
                 if (attacker.IsOgre)
                 { 
                     target = target.SetOgre(true);
                     targetGameEventListener?.OnBecomeOgre();

                     attacker = attacker.SetOgre(false);
                     var attackerNetworkObj = runner.GetPlayerObject(target.PlayerRef);
                     var attackerGameEventListener = attackerNetworkObj.GetComponent<IGameEventListener>();
                     attackerGameEventListener?.OnBecomeNormal();
                 }
             }
             
             //データベースへの更新
             PlayerDatabase.Instance.UpdateDatabase(attacker);
             PlayerDatabase.Instance.UpdateDatabase(target);
        }
    }
}