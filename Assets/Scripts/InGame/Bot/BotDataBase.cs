using System.Collections.Generic;
using InGame.Exhibit;
using InGame.Interact;
using InGame.Player;
using Result;
using September.Common;
using UnityEngine;

namespace InGame.Bot
{
    public class BotDataBase : MonoBehaviour
    {
        [SerializeField] private float _randomDistanceAmount = 50f;
        public static BotDataBase Instance;
        private HashSet<InteractableBase> _exhibitObjects;
        private HashSet<InteractableBase> _rideObjects;
        private PlayerManager[] _playerManagers;

        public void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
        }

        public void Start()
        {
            //インタラクトオブジェクトを入れる
        }

        public PlayerManager GetNearbyPlayer(GameObject myObject)
        {
            if (_playerManagers == null)
            {
                SetPlayerManager();
            }

            float minDistance = float.MaxValue;
            PlayerManager player = null;
            foreach (var p in _playerManagers)
            {
                if (p == null || p.gameObject == myObject || p.IsStun)
                    continue;

                float dis = (myObject.transform.position - p.transform.position).sqrMagnitude;
                if (minDistance > dis)
                {
                    minDistance = dis;
                    player = p;
                }
            }

            return player;
        }
        public InteractableBase GetNearbyInteractable(GameObject myObject)
        {
            if (_exhibitObjects == null || _exhibitObjects.Count == 0)
            {
                SetInteractData();
            }

            float minDistance = float.MaxValue;
            InteractableBase interactObject = null;
            foreach (var p in _exhibitObjects)
            {
                if (p == null || p.IsInCooldown())
                    continue;

                float dis = (myObject.transform.position - p.transform.position).sqrMagnitude;
                float randomAmount = Random.Range(0f, _randomDistanceAmount);
                dis += randomAmount * randomAmount;
                if (minDistance > dis)
                {
                    minDistance = dis;
                    interactObject = p;
                }
            }

            return interactObject;
        }

        private void SetPlayerManager()
        {
            _playerManagers = new PlayerManager[PlayerDatabase.Instance.PlayerObjectDic.Count];
            int index = 0;
            foreach (var kv in PlayerDatabase.Instance.PlayerObjectDic)
            {
                _playerManagers[index] = kv.Value.GetComponent<PlayerManager>();
                index++;
            }
        }

        private void SetInteractData()
        {
            _rideObjects = new();
            _exhibitObjects = new();

            foreach (var interactObj in ExhibitRegistry.I.Items)
            {
                //ライドとそれ以外に分類する
                switch (interactObj.ExhibitType)
                {
                    case ExhibitType.Ptr:
                    case ExhibitType.TRex:
                    case ExhibitType.AirPlane:
                    case ExhibitType.Ballista:
                    case ExhibitType.Cannon:
                    case ExhibitType.Shark:
                    case ExhibitType.Kraken:
                        _rideObjects.Add(interactObj);
                        break;
                    case ExhibitType.Art:
                    case ExhibitType.FlagealCamouflage:
                    case ExhibitType.Tutankhamun:
                    case ExhibitType.LondonTelephone:
                    case ExhibitType.Car:
                    case ExhibitType.Moai:
                    case ExhibitType.Instrument:
                    case ExhibitType.Muramasa:
                    case ExhibitType.SateliteCanon:
                    case ExhibitType.Mast:
                    case ExhibitType.ZipLine:
                    case ExhibitType.Armory:
                    case ExhibitType.NauticalChart:
                        _exhibitObjects.Add(interactObj);
                        break;
                    case ExhibitType.None:
                    default:
                        Debug.LogError($"未対応の ExhibitType です: {interactObj.ExhibitType} ({interactObj.name})");
                        break;
                }
            }
        }
    }
}
