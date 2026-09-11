using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Fusion;
using September.InGame.Kraken.Attack;
using UnityEngine;

namespace September.InGame.Kraken.Animations
{
    /// <summary>
    /// 触手攻撃を管理するクラス
    /// </summary>
    public class KrakenAttackHandler : NetworkBehaviour
    {
        private TentacleController[] _tentacles;

        public bool IsReady => TryGetTentacle(out _);

        public void Initialize(IReadOnlyList<ArmSettings> arms, KrakenSettings krakenSettings, Kraken kraken)
        {
            _tentacles = new TentacleController[arms.Count];
            for (int i = 0; i < arms.Count; i++)
            {
                _tentacles[i] = new TentacleController(arms[i], krakenSettings, kraken);
            }
        }

        public override void Render()
        {
            if (_tentacles == null) return;

            foreach (var tentacle in _tentacles)
            {
                tentacle.Tick(Runner);
            }
        }

        public bool TryGetTentacle(out TentacleController result)
        {
            if (_tentacles == null)
            {
                result = null;
                return false;
            }

            foreach (var tentacle in _tentacles)
            {
                if (!tentacle.IsAttacking)
                {
                    result = tentacle;
                    return true;
                }
            }

            result = null;
            return false;
        }

        public async UniTask Attack(TentacleController tentacle, Vector3 target)
        {
            Debug.Log("Start Attack");
            tentacle.LookAt(target);
            tentacle.StartAttack(Runner);

            await tentacle.PlayAnimation(destroyCancellationToken);

            tentacle.ResetState();
            Debug.Log("End Attack");
        }

        public void StartAreaAttack(TentacleController tentacle, PredictionParticle particle)
        {
            tentacle.StartAreaAttack(Runner, particle);
        }
    }
}
