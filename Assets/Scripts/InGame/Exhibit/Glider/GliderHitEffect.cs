using Fusion;
using September.InGame.Exhibit;
using UnityEngine;

namespace September
{
    public class GliderHitEffect : IProjectileHitEffect
    {
        [SerializeField] private GliderBomb _bomb;
        [SerializeField] private float _explodeSeconds;
        [SerializeField] private float _explodeRadius;
        [SerializeField] private int _explodeDamage;
        NetworkRunner _networkRunner;
        public void Initialize(NetworkRunner runner)
        {
            _networkRunner = runner;
        }

        public void OnStateAuthorityHit(Vector3 hitPos, Vector3 normal, GameObject hitObject, PlayerRef usePlayer)
        {
            _networkRunner.Spawn(_bomb,
                position: hitPos,
                rotation: Quaternion.LookRotation(normal),
                onBeforeSpawned: (runner, obj) =>
                {
                    var bomb = obj.GetComponent<GliderBomb>();
                    bomb.Initialize(_explodeSeconds, _explodeRadius, _explodeDamage, usePlayer);
                });
        }

        public void OnHit(Vector3 hitPos, Vector3 normal)
        {
            
        }

        public void DrawGizmos(Vector3 hitPos, Vector3 normal)
        {
            
        }
    }
}
