using System;
using UnityEngine;
using Fusion;
using System.Collections.Generic;
using InGame.Common;
using September.Common;

namespace September.InGame.Effect
{
    public class EffectSpawner : NetworkBehaviour
    {
        private NetworkRunner _networkRunner;
        private EffectDatabase _effectDatabase;
        private Dictionary<string, GameObject> _activeEffects; //IDは呼び出し側に作ってもらう

        
        private void Awake()
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
            
            StaticServiceLocator.Instance.Register<EffectSpawner>(this);
        }

        /// <summary>
        /// ループせずに生成するエフェクト
        /// </summary>
        public void RequestPlayOneShotEffect(EffectType effectType, Vector3 position, Quaternion rotation)
        {
            RPC_PlayEffect(effectType, position, rotation, false, string.Empty);
        }

        /// <summary>
        /// ループせずに生成するエフェクト（親オブジェクト指定）
        /// </summary>
        public void RequestPlayOneShotEffect(EffectType effectType, Vector3 position, Quaternion rotation, Transform parent)
        {
            RPC_PlayEffect(effectType, position, rotation, false, string.Empty, parent);
        }

        /// <summary>
        /// 手動で削除するエフェクトのリクエスト
        /// </summary>
        /// <param name="effectId">ユーザー名＋タイムスタンプ推奨</param>
        public void RequestPlayLoopEffect(string effectId, EffectType effectType, Vector3 position, Quaternion rotation)
        {
            RPC_PlayEffect(effectType, position, rotation, true, effectId);
        }

        /// <summary>
        /// 手動で削除するエフェクトのリクエスト（親オブジェクト指定）
        /// </summary>
        /// <param name="effectId">ユーザー名＋タイムスタンプ推奨</param>
        public void RequestPlayLoopEffect(string effectId, EffectType effectType, Vector3 position, Quaternion rotation, Transform parent)
        {
            RPC_PlayEffect(effectType, position, rotation, true, effectId, parent);
        }

        /// <summary>
        /// 指定されたIDのエフェクトを停止する
        /// </summary>
        public void StopEffect(string effectId)
        {
            RPC_StopEffectById(effectId);
        }

        /// <summary>
        /// 統一されたエフェクト再生RPC
        /// </summary>
        /// <param name="effectType">エフェクトタイプ</param>
        /// <param name="position">生成位置</param>
        /// <param name="rotation">生成回転</param>
        /// <param name="isLoop">ループするかどうか</param>
        /// <param name="effectId">エフェクトID（ループエフェクトの場合のみ使用）</param>
        [Rpc(RpcSources.All, RpcTargets.All)]
        private void RPC_PlayEffect(EffectType effectType, Vector3 position, Quaternion rotation, bool isLoop, string effectId, Transform parent = null)
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

            GameObject effect;
            if (parent != null)
            {
                // 親オブジェクトが指定されている場合、子オブジェクトとして生成
                effect = Instantiate(effectData.Prefab, position, rotation, parent);
                Debug.Log($"エフェクト '{effectType}' を親オブジェクト '{parent.name}' の子として生成");
            }
            else
            {
                // 親オブジェクトが指定されていない場合、通常通り生成
                effect = Instantiate(effectData.Prefab, position, rotation);
                Debug.Log($"エフェクト '{effectType}' を単独で生成");
            }

            //パーティクルシステムの設定
            ParticleSystem system = effect.GetComponent<ParticleSystem>();
            if (system != null)
            {
                var main = system.main;
                main.loop = isLoop;
                
                if (isLoop)
                {
                    // ループエフェクトの場合
                    system.Play();
                    _activeEffects[effectId] = effect;
                    Debug.Log($"ループエフェクトID '{effectId}' を生成");
                }
                else
                {
                    // ワンショットエフェクトの場合
                    main.stopAction = ParticleSystemStopAction.Destroy;
                    system.Play();
                    Debug.Log($"ワンショットエフェクト '{effectType}' を生成");
                }
            }
            else
            {
                Debug.LogWarning($"エフェクト '{effectType}' にParticleSystemが見つかりません");
                
                // ループエフェクトでParticleSystemがない場合も辞書に追加
                if (isLoop && !string.IsNullOrEmpty(effectId))
                {
                    _activeEffects[effectId] = effect;
                    Debug.Log($"非パーティクルループエフェクトID '{effectId}' を生成");
                }
            }
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