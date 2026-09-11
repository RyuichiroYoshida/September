using UnityEngine;

namespace September.InGame.Kraken.Attack
{
    /// <summary>
    /// 攻撃予測ファクトリ
    /// </summary>
    public class AttackPredictionFactory : MonoBehaviour
    {
        [SerializeField] private GameObject _predictionParticlePrefab;

        private GameObject GetPredictionParticle()
        {
            return Instantiate(_predictionParticlePrefab);
        }

        public PredictionParticle Create(AttackPredictionShape shape)
        {
            GameObject predictionParticle = GetPredictionParticle();
            predictionParticle.transform.position = shape.Position;
            predictionParticle.transform.rotation = shape.Rotation;
            predictionParticle.transform.localScale = shape.HalfExtents;
            return new PredictionParticle(predictionParticle, shape);
        }
    }

    /// <summary>
    /// 攻撃予測の形状を設定する構造体
    /// </summary>
    public struct AttackPredictionShape
    {
        public Vector3 Position;
        public Vector3 HalfExtents;
        public Quaternion Rotation;

        public AttackPredictionShape(Vector3 position, Vector3 halfExtents, Quaternion rotation)
        {
            Position = position;
            HalfExtents = halfExtents;
            Rotation = rotation;
        }
    }

    /// <summary>
    /// 予測表示オブジェクトの実体を保持し、外部から操作可能にするための構造体
    /// </summary>
    public readonly struct PredictionParticle
    {
        private readonly GameObject _predictionParticle;
        private readonly AttackPredictionShape _shape;

        public Vector3 ForwardEndPos => _shape.Position +
                                        _shape.Rotation * Vector3.forward * _shape.HalfExtents.z;

        public Vector3 BackEndPos => _shape.Position +
                                     _shape.Rotation * Vector3.back * _shape.HalfExtents.z;

        public PredictionParticle(GameObject predictionParticle, AttackPredictionShape shape)
        {
            _predictionParticle = predictionParticle;
            _shape = shape;
        }

        public void Destroy()
        {
            Object.Destroy(_predictionParticle);
        }
    }
}
