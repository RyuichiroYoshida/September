using UnityEngine;

namespace September.OgreSystem
{
    public class SampleOgreSystem : MonoBehaviour, IGameEventListener
    {
        private OgreManager _ogreManager;
        private IGameEventListener _gameEventListenerImplementation;

        void Awake()
        {
            _ogreManager = OgreManager.Instance;
        }
        
        void Start()
        {
            //登録
            int id = IDGenerator.GenerateID();
            
            //var playerData = new PlayerData(id, "shiomi", 20, 20, false, false);
            //_ogreManager.Register(playerData);
        }

        public void Register()
        {
            //登録
            int id = IDGenerator.GenerateID();
            
            //var playerData = new PlayerData(id, "okabe", 20, 20, false, false);
            //_ogreManager.Register(playerData);
        }

        //鬼の抽選
        public void Select()
        {
            _ogreManager.ChooseOger();
        }


        //鬼からの攻撃
        public void Attack()
        {
            //_ogreManager.SetHp("player0002", "player0001", 1000);
        }

        public void OnBecomeOgre()
        {
            Debug.Log("鬼になった");
        }

        public void OnParalyzed()
        {
            Debug.Log("気絶した");
        }

        public void OnBecomeNormal()
        {
            Debug.Log("普通に戻った");
        }
    }
}

