using System;
using System.Linq;
using Fusion;
using InGame.Common;
using NaughtyAttributes;
using September.Common;
using September.InGame;
using UnityEngine;
using Object = UnityEngine.Object;

namespace InGame.Player.Ability
{
    /// <summary>
    /// 床生成アビリティ
    /// プレイヤーの位置に一時的な床オブジェクトを生成します。
    /// クールダウン時間が経過すると自動的に床を削除します。
    /// </summary>
    [Serializable]
    public class AbilityGenerateFloor : AbilityBase
    {
        private const string FLOOR_PREFAB_GUID = "faf9ec27ccd233e428d9e66595732aef";
        private const int INVALID_SPAWN_ID = -1;

        private int _spawnedObjectId = INVALID_SPAWN_ID;
        private PlayerManager _sourcePlayer;

        public override string DisplayName => "床生成";
        public override bool RunLocal => false;

        public AbilityGenerateFloor()
        {
        }

        public AbilityGenerateFloor(AbilityBase abilityReference) : base(abilityReference) { }

        public override AbilityBase Clone(AbilityBase abilityBase) => new AbilityGenerateFloor(this);

        public override void InitAbility(AbilityContext context, ISpawner spawner)
        {
            base.InitAbility(context, spawner);
            var players = Object.FindObjectsByType<PlayerManager>(FindObjectsSortMode.None).FirstOrDefault(
                player =>
                {
                    var networkObject = player.GetComponent<NetworkObject>();
                    return networkObject != null && networkObject.InputAuthority.RawEncoded == Context.SourcePlayer;
                });
            _sourcePlayer = players;
            
            var sharedState = new AbilitySharedState
            {
                AbilityName = AbilityName,
                OwnerPlayerId = OwnerPlayerId,
                IsFloorActive = 1,
            };
            
            StartCooldown(_cooldown);
            StaticServiceLocator.Instance.Get<IAbilityExecutor>().ApplyAbilityState(sharedState);

            if (_sourcePlayer == null)
            {
                Debug.LogError($"[{nameof(AbilityGenerateFloor)}] Source player not found for ability initialization");
            }
        }

        protected override void OnStart()
        {
            if (!ValidateComponents())
            {
                ForceEnd();
                return;
            }

            var spawnTransform = CalculateSpawnTransform();
            _spawnedObjectId = _spawner.Spawn(FLOOR_PREFAB_GUID, spawnTransform.position, spawnTransform.rotation);

            if (_spawnedObjectId == INVALID_SPAWN_ID)
            {
                Debug.LogError($"[{nameof(AbilityGenerateFloor)}] Failed to spawn floor object");
                ForceEnd();
                return;
            }
        }

        protected override void OnUpdate(float deltaTime)
        {
            if (!IsCooldown)
            {
                ForceEnd();
            }
        }

        public override void OnEndAbility()
        {
            CleanupSpawnedObject();
        }
        
        private bool ValidateComponents()
        {
            if (_spawner == null)
            {
                Debug.LogError($"[{nameof(AbilityGenerateFloor)}] Spawner is null");
                return false;
            }

            if (_sourcePlayer == null)
            {
                Debug.LogError($"[{nameof(AbilityGenerateFloor)}] Source player is null");
                return false;
            }

            return true;
        }

        private (Vector3 position, Quaternion rotation) CalculateSpawnTransform()
        {
            if (_sourcePlayer == null)
            {
                Debug.LogWarning($"[{nameof(AbilityGenerateFloor)}] Using default spawn position due to null player");
                return (Vector3.zero, Quaternion.identity);
            }

            var playerTransform = _sourcePlayer.transform;
            var spawnPosition = new Vector3(playerTransform.position.x, playerTransform.position.y,
                playerTransform.position.z);
            var spawnRotation = Quaternion.LookRotation(playerTransform.forward, Vector3.up);
            return (spawnPosition, spawnRotation);
        }

        private void CleanupSpawnedObject()
        {
            if (_spawnedObjectId == INVALID_SPAWN_ID || _spawner == null)
                return;

            try
            {
                _spawner.Despawn(_spawnedObjectId);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[{nameof(AbilityGenerateFloor)}] Failed to despawn object: {ex.Message}");
            }
            finally
            {
                _spawnedObjectId = INVALID_SPAWN_ID;
            }
        }
    }
}