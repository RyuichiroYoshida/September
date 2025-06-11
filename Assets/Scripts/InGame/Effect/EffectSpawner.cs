using System;
using UnityEngine;
using Fusion;
using System.Collections.Generic;

namespace September.InGame.Effect
{
    public class EffectSpawner : NetworkBehaviour
    {
        private NetworkRunner _networkRunner;
        private EffectDatabase _effectDatabase;

        private Dictionary<int, GameObject> _activeEffects;
        private int _effectID = 0;
        
        // コールバック管理用
        private Dictionary<int, Action<int>> _pendingCallbacks = new Dictionary<int, Action<int>>();

        private void Start()
        {
            _networkRunner = FindFirstObjectByType<NetworkRunner>();
            if (_networkRunner == null)
            {
                Debug.LogError("NetworkRunnerがありません");
            }
            if (!_networkRunner.IsServer) return;
            
            // ScriptableObject をクライアント・ホスト問わずロード
            InitializeEffectDatabase();
        }
        
        private void InitializeEffectDatabase()
        {
            if (_effectDatabase == null)
            {
                _effectDatabase = Resources.Load<EffectDatabase>("EffectDatabase");
                if (_effectDatabase == null)
                {
                    Debug.LogError("Resources/EffectDatabase が見つかりません");
                }
                else
                {
                    Debug.Log($"EffectDatabase ロード成功");
                }
            }
            
            if (_activeEffects == null)
                _activeEffects = new Dictionary<int, GameObject>();
        }

        /// <summary>
        /// 一度だけ発火するエフェクト
        /// </summary>
        /// <param name="effectType"></param>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        /// <param name="effectSpqwnSettings"></param>
        public void RequestPlayEffect(EffectType effectType, Vector3 position, Quaternion rotation, EffectSpqwnSettings effectSpqwnSettings = null)
        {
            RPC_SimplePlayEffect(effectType, position, rotation, effectSpqwnSettings);
        }
        
        /// <summary>
        /// 手動で削除するエフェクトのリクエスト
        /// </summary>
        public void RequestPlayEffect(EffectType effectType, Vector3 position, Quaternion rotation, Action<int> onEffectCreated = null, EffectSpqwnSettings effectSpqwnSettings = null)
        {
            // サーバーでのみID生成とRPC送信
            if (_networkRunner.IsServer)
            {
                int id = _effectID++;
                if (onEffectCreated != null)
                {
                    _pendingCallbacks[id] = onEffectCreated;
                }
                RPC_PlayEffect(effectType, position, rotation, id);
            }
            else
            {
                // クライアントの場合、コールバックIDを生成して送信
                int callbackId = UnityEngine.Random.Range(10000, 99999); // 一時的なコールバックID
                if (onEffectCreated != null)
                {
                    _pendingCallbacks[callbackId] = onEffectCreated;
                }
                RPC_RequestPlayEffect(effectType, position, rotation, callbackId);
            }
        }

        /// <summary>
        /// 指定されたIDのエフェクトを停止する
        /// </summary>
        /// <param name="instanceId"></param>
        public void StopEffect(int instanceId)
        {
            RPC_RequestStopEffect(instanceId);
        }

        // クライアントからのリクエストを受ける（サーバーのみ）
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestPlayEffect(EffectType effectType, Vector3 position, Quaternion rotation, int callbackId)
        {
            int id = _effectID++;
            RPC_PlayEffect(effectType, position, rotation, id);
            
            // クライアントにIDを通知
            RPC_NotifyEffectCreated(id, callbackId);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestStopEffect(int instanceId)
        {
            RPC_StopEffectById(instanceId);
        }

        //エフェクトの発火
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_PlayEffect(EffectType effectType, Vector3 position, Quaternion rotation, int instanceId)
        {
            if (_effectDatabase == null)
            {
                InitializeEffectDatabase();
            }
            
            var effectData = _effectDatabase.GetEffectData(effectType);
            
            if (effectData.Prefab == null)
            {
                Debug.LogError($"'{effectType}' に対応するプレハブが見つかりません");
                return;
            }
            
            GameObject effect = Instantiate(effectData.Prefab, position, rotation);
            _activeEffects[instanceId] = effect;
            Debug.Log($"エフェクトID '{instanceId}' を生成");
            
            // コールバック実行（サーバー側）
            if (_pendingCallbacks.TryGetValue(instanceId, out Action<int> callback))
            {
                callback?.Invoke(instanceId);
                _pendingCallbacks.Remove(instanceId);
            }
        }
        
        // クライアントへのID通知用RPC
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_NotifyEffectCreated(int effectId, int callbackId)
        {
            if (_pendingCallbacks.TryGetValue(callbackId, out Action<int> callback))
            {
                callback?.Invoke(effectId);
                _pendingCallbacks.Remove(callbackId);
            }
        }

        //音をPlay
        [Rpc(RpcSources.All, RpcTargets.All)]
        private void RPC_SimplePlayEffect(EffectType effectType, Vector3 position, Quaternion rotation, EffectSpqwnSettings effectSpqwnSettings = null)
        {
            if (_effectDatabase == null)
            {
                InitializeEffectDatabase();
            }
            
            var effectData = _effectDatabase.GetEffectData(effectType);
            
            if (effectData.Prefab == null)
            {
                Debug.LogError($"'{effectType}' に対応するプレハブが見つかりません");
                return;
            }
            
            GameObject effect = Instantiate(effectData.Prefab, position, rotation);
            ParticleSystem particleSystem = effect.GetComponent<ParticleSystem>();
            
            var main = particleSystem.main;
            main.loop = false;
        }
        
        //エフェクトを止める
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_StopEffectById(int instanceId)
        {
            if (_activeEffects.TryGetValue(instanceId, out GameObject effect))
            {
                if (effect != null)
                {
                    Destroy(effect);
                }
                _activeEffects.Remove(instanceId);
                Debug.Log($"エフェクトID '{instanceId}' を停止");
            }
        }

        private void OnDestroy()
        {
            // クリーンアップ
            if (_activeEffects != null)
            {
                foreach (var effect in _activeEffects.Values)
                {
                    if (effect != null)
                    {
                        Destroy(effect);
                    }
                }
                _activeEffects.Clear();
            }
            
            _pendingCallbacks?.Clear();
        }
    }
}