using UnityEngine;

namespace September.OgreSystem
{
    public class SampleOgreSystem : MonoBehaviour
    {
        private OgreManager _ogreManager;

        void Awake()
        {
            _ogreManager = OgreManager.Instance;
        }

        //鬼の抽選
        public void Select()
        {
            _ogreManager.ChooseOgre();
        }


        //鬼からの攻撃
        public void Attack(int targetID)
        {
            if (targetID == 1)
            {
                _ogreManager.SetHp(targetID, 2, 5);
            }
            else
            {
                _ogreManager.SetHp(targetID, 1, 5);
            }
        }
    }
}

