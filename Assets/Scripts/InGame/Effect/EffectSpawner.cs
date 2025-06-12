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
        private Dictionary<string, GameObject> _activeEffects; //IDは呼び出し側に作ってもらう

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
        
        //初期化処理
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
                _activeEffects = new Dictionary<string, GameObject>();
        }

        /// <summary>
        /// ループせずに生成するエフェクト
        /// </summary>
        public void RequestPlayOneShotEffect(EffectType effectType, Vector3 position, Quaternion rotation)
        {
            RPC_PlayOneShotEffect(effectType, position, rotation);
        }
        
        /// <summary>
        /// 手動で削除するエフェクトのリクエスト
        /// </summary>
        /// <param name="effectId">ユーザー名＋タイムスタンプ推奨</param>
        public void RequestPlayLoopEffect(string effectId, EffectType effectType, Vector3 position, Quaternion rotation)
        {
            RPC_PlayLoopEffect(effectId, effectType, position, rotation);
        }

        /// <summary>
        /// 指定されたIDのエフェクトを停止する
        /// </summary>
        public void StopEffect(string effectId)
        {
            RPC_StopEffectById(effectId);
        }

        //エフェクトの発火
        [Rpc(RpcSources.All, RpcTargets.All)]
        private void RPC_PlayLoopEffect(string effectId, EffectType effectType, Vector3 position, Quaternion rotation)
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
            
            //パーティクルシステムの設定
            ParticleSystem system = effect.GetComponent<ParticleSystem>();
            var main = system.main;
            main.loop = true;
            system.Play();
            
            _activeEffects[effectId] = effect;
            Debug.Log($"エフェクトID '{effectId}' を生成");
        }

        //エフェクト処理を全体に通知する
        [Rpc(RpcSources.All, RpcTargets.All)]
        private void RPC_PlayOneShotEffect(EffectType effectType, Vector3 position, Quaternion rotation)
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
            
            //パーティクルシステムの設定
            ParticleSystem system = effect.GetComponent<ParticleSystem>();
            var main = system.main;
            main.loop = false;
            main.stopAction = ParticleSystemStopAction.Destroy;
            system.Play();
        }
        
        //エフェクトを止める
        [Rpc(RpcSources.All, RpcTargets.All)]
        private void RPC_StopEffectById(string effectId)
        {
            if (_activeEffects.TryGetValue(effectId, out GameObject effect))
            {
                if (effect != null)
                {
                    Destroy(effect);
                }
                _activeEffects.Remove(effectId);
                Debug.Log($"エフェクトID '{effectId}' を停止");
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