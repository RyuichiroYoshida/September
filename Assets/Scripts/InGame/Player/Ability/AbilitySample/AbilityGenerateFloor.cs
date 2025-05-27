using System;
using System.Linq;
using Fusion;
using NaughtyAttributes;
using September.InGame;
using UnityEngine;

namespace InGame.Player.Ability
{
    /// <summary>
    /// サンプル用のアビリティクラス
    /// 単発でオブジェクトを生成する系のアビリティのサンプルです。
    /// </summary>
    [Serializable]
    public class AbilityGenerateFloor : AbilityBase
    {
        [SerializeField, Label("生成する床")] private GameObject _floorPrefab;
        private int _spawnObjId = -1;
        private const string FloorPrefabGuid = "faf9ec27ccd233e428d9e66595732aef";

        public override string DisplayName => "床生成";
        public override bool RunLocal => false;

        public AbilityGenerateFloor() {}
        public AbilityGenerateFloor(AbilityBase abilityReference) : base(abilityReference)
        {
            _floorPrefab = ((AbilityGenerateFloor)abilityReference)._floorPrefab;
        }

        public override AbilityBase Clone(AbilityBase abilityBase) => new AbilityGenerateFloor(this);

        protected override void OnStart()
        {
            var player = GameObject.FindObjectsByType<PlayerController>(FindObjectsSortMode.None).Select(x => x.GetComponent<NetworkObject>())
                .FirstOrDefault(x => x.InputAuthority == Context.SourcePlayer);
            var pos = player ? player.transform.position : Vector3.zero;
            var rot = Quaternion.Euler(player ? player.transform.forward : Vector3.zero);
            _spawnObjId = _spawner.Spawn(FloorPrefabGuid, pos, rot);
        }

        protected override void OnUpdate(float deltaTime)
        {
            if (!IsCooldown) ForceEnd();
        }

        public override void OnEndAbility()
        {
            if (_spawnObjId != -1 && _spawner != null)
            {
                _spawner.Despawn(_spawnObjId);
                _spawnObjId = -1;
            }
        }
    }
}
