using System.Collections.Generic;
using System.Linq;

namespace September.OgreSystem
{
    public class PlayerDatabase
    {
        private static PlayerDatabase _instance;

        public static PlayerDatabase Instatnce
        {
            get
            {
                if (_instance == null)
                    _instance = new PlayerDatabase();
                return _instance;
            }
        }

        private Dictionary<string, PlayerData> _playerDictionary;

        private PlayerDatabase()
        {
            _playerDictionary = new Dictionary<string, PlayerData>();
        }

        /// <summary>
        /// プレイヤーデータを登録する
        /// </summary>
        /// <param name="data">プレイヤーデータ</param>
        public void Register(PlayerData data)
        {
            _playerDictionary[data.ID] = data;
        }

        /// <summary>
        /// プレイヤーデータを取得
        /// </summary>
        /// <param name="id">ID（キー）</param>
        /// <param name="data">データ</param>
        /// <returns></returns>
        public bool TryGetPlayerData(string id, out PlayerData data)
        {
            return _playerDictionary.TryGetValue(id, out data);
        }

        
        /// <summary>
        /// プレイヤーデータの更新
        /// </summary>
        /// <param name="playerData"></param>
        public void Update(PlayerData playerData)
        {
            _playerDictionary[playerData.ID] = playerData;
        }

        /// <summary>
        /// すべてのデーターを取得
        /// </summary>
        /// <returns></returns>
        public List<PlayerData> GetAll()
        {
            return _playerDictionary.Values.ToList();
        }
    }
}

