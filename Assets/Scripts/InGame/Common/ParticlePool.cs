using System;
using System.Collections.Generic;
using UniRx;
using UnityEngine;
using UniRx.Toolkit;
using Observable = UniRx.Observable;

namespace InGame.Common
{
    public static class ParticlePool
    {
        private static readonly Dictionary<ParticleSystem, Pool> _pools = new();
        private static Transform _parent;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            _parent = new GameObject("PoolParticleParent").transform;
            GameObject.DontDestroyOnLoad(_parent.gameObject);
        }

        public static ParticleSystem Play(ParticleSystem prefab, Vector3 position, Quaternion rotation, float? duration = null)
        {
            var particle = RentAndPlay(prefab, duration);
            particle.transform.SetParent(_parent);
            particle.transform.position = position;
            particle.transform.rotation = rotation;
            return particle;
        }
        
        public static ParticleSystem Play(ParticleSystem prefab, Transform parent, float? duration = null)
        {
            var particle = RentAndPlay(prefab, duration);
            particle.transform.SetParent(parent);
            return particle;
        }

        static ParticleSystem RentAndPlay(ParticleSystem prefab, float? duration = null)
        {
            if (!_pools.TryGetValue(prefab, out var pool))
            {
                pool = new Pool(prefab);
                _pools[prefab] = pool;
            }
            
            var particle = pool.Rent();
            particle.gameObject.SetActive(true);
            particle.Play();
            
            Observable.Timer(TimeSpan.FromSeconds(duration ?? particle.main.duration + particle.main.startLifetime.constantMax))
                .TakeUntilDisable(particle.gameObject)
                .Subscribe(_ =>
                {
                    Return(particle, prefab);
                })
                .AddTo(particle);
            
            return particle;
        }

        static void Return(ParticleSystem particle, ParticleSystem prefab)
        {
            particle.Stop();
            particle.gameObject.SetActive(false);

            if (_pools.TryGetValue(prefab, out var pool))
            {
                pool.Return(particle);
            }
            else
            {
                Debug.LogWarning($"Prefab の pool が無い : {prefab.name}");
                GameObject.Destroy(particle.gameObject);
            }
        }
        
        private class Pool : ObjectPool<ParticleSystem>
        {
            private readonly ParticleSystem _particlePrefab;
        
            public Pool(ParticleSystem particlePrefab)
            {
                _particlePrefab = particlePrefab;
            }

            protected override ParticleSystem CreateInstance()
            {
                return GameObject.Instantiate(_particlePrefab);
            }
        }
    }
}
