using System;
using System.Collections.Generic;
using UnityEngine;

namespace September.Common
{
    public static class StaticServiceLocator
    {
        public static readonly ServiceLocator Instance = new ServiceLocator();
    }
    
    public class ServiceLocator
    {
        private readonly Dictionary<Type, object> _services = new();

        public void Register<T>(T instance)
        {
            var type = typeof(T);
            if (_services.ContainsKey(type))
            {
                UnityEngine.Debug.LogWarning($"{type.Name} is already registered. Overwriting.");
            }

            _services[type] = instance;
        }
        
        public void Register(System.Type type, object instance)
        {
            _services[type] = instance;
        }

        public T Get<T>()
        {
            if (_services.TryGetValue(typeof(T), out var service))
                return (T)service;

            throw new InvalidOperationException($"{typeof(T).Name} is not registered.");
        }

        public bool TryGet<T>(out T result)
        {
            if (_services.TryGetValue(typeof(T), out var service))
            {
                result = (T)service;
                return true;
            }

            result = default;
            return false;
        }

        public void Clear() => _services.Clear();
    }
}
