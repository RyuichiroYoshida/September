using UnityEngine;
using Fusion;
using System.Collections.Generic;

namespace September.InGame.Effect
{
    public class EffectSpawner : NetworkBehaviour
    {
        private NetworkRunner _networkRunner;
        private EffectDatabase _effectDatabase {get; set;}

        private Dictionary<int, GameObject> _activeEffects;
        private int _effectID = 0; //サーバーのみが使う
        
        public event System.Action<int> OnEffectPlayed;   

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
        /// エフェクトのリクエスト
        /// </summary>
        public void RequestPlayEffect(EffectType effectType, Vector3 position, Quaternion rotation)
        {
            if (_networkRunner.IsServer)
            {
                // サーバー自身なら直接再生
                int id = _effectID++;
                RPC_PlayEffect(effectType, position, rotation, id);
                
                OnEffectPlayed?.Invoke(id); // 呼び出し元に通知
            }
            else
            {
                // クライアントはサーバーに依頼
                RPC_RequestPlayEffect(effectType, position, rotation);
            }
        }
        
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RPC_RequestPlayEffect(EffectType effectType, Vector3 position, Quaternion rotation, RpcInfo info = default)
        {
            int id = _effectID++;
            RPC_PlayEffect(effectType, position, rotation, id);
        }

        /// <summary>
        /// 指定されたIDのエフェクトを停止する
        /// </summary>
        /// <param name="instanceId"></param>
        public void StopEffect(int instanceId)
        {
            RPC_StopEffectById(instanceId);
        }

        //エフェクトの発火
        [Rpc(RpcSources.All, RpcTargets.All)]
        private void RPC_PlayEffect(EffectType effectType, Vector3 position, Quaternion rotation, int instanceId)
        {
            // 安全性チェック
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
        }
        
        //指定されたIDのエフェクトを削除
        [Rpc(RpcSources.All, RpcTargets.All)]
        private void RPC_StopEffectById(int instanceId)
        {
            if (_activeEffects.TryGetValue(instanceId, out GameObject effect))
            {
                if (effect != null)
                {
                    Destroy(effect);
                }
                else
                {
                    Debug.LogError($"インスタンスID'{instanceId}' に対応するプレハブが見つかりません");
                }
                _activeEffects.Remove(instanceId);
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
        }
    }
}