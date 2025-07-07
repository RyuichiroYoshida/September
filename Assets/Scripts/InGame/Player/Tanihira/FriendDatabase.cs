using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ingame.Tanihira
{
    [CreateAssetMenu(fileName = "FriendDatabase", menuName = "ScriptableObjects/FriendDatabase")]
    public class FriendDatabase : ScriptableObject
    {
        [Serializable]
        public struct FriendData
        {
            public FriendType _type;
            public GameObject _friendObject;
        }

        [SerializeField] private List<FriendData> _friendDataList;

        /// <summary>
        /// フレンドのプレハブを取得する
        /// </summary>
        public GameObject GetFriendObject(FriendType type)
        {
            foreach (var data in _friendDataList)
            {
                if (data._type == type)
                {
                    return data._friendObject;
                }
            }

            Debug.LogWarning($"FriendType {type} not found in FriendDatabase.");
            return null;
        }
    }
}