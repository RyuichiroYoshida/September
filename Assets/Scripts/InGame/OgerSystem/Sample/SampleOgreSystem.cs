using UnityEngine;

namespace September.OgerSystem
{
    public class SampleOgreSystem : MonoBehaviour, IGameEventListener
    {
        private OgerManager ogerManager;
        private IGameEventListener _gameEventListenerImplementation;

        void Awake()
        {
            ogerManager = OgerManager.Instance;
        }
        
        void Start()
        {
            //登録
            string id = IDGenerator.GenerateID();
            
            var playerData = new PlayerData(id, "shiomi", 20, 20, false, false, this);
            ogerManager.Register(playerData);
        }

        public void Register()
        {
            //登録
            string id = IDGenerator.GenerateID();
            
            var playerData = new PlayerData(id, "okabe", 20, 20, false, false, this);
            ogerManager.Register(playerData);
        }

        //鬼の抽選
        public void Select()
        {
            ogerManager.ChooseOger();
        }


        //鬼からの攻撃
        public void Attack()
        {
            ogerManager.SetHp("player0002", "player0001", 1000);
        }

        public void OnBecomeOger()
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

