using System.Threading.Tasks;
using UnityEngine;

namespace September.InGame.Effect
{
    public interface IEffectSpawner
    {
        Task<GameObject> RequestPlayEffectAsync(EffectType effectType, Vector3 position, Quaternion rotation);
        void StopEffect(int instanceId);
    }
}