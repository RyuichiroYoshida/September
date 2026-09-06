using UnityEngine;
using Fusion;
using System.Collections.Generic;
using September.Common;

namespace September.InGame.Effect
{
    public class EffectSpawner : NetworkBehaviour
    {
        private NetworkRunner _networkRunner;
        private EffectDatabase _effectDatabase;
        private Dictionary<EffectID, GameObject> _activeEffects;

        private int _effectCount = 0;

        private void Awake()
        {
                  
            StaticServiceLocator.Instance.Register<EffectSpawner>(this);
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
                _activeEffects = new Dictionary<EffectID, GameObject>();
      
        }

        /// <summary>
        /// ループせずに生成するエフェクト
        /// </summary>
        public void RequestPlayOneShotEffect(EffectType effectType, Vector3 position, Quaternion rotation)
        {
            if (effectType == EffectType.None) return;

            RPC_PlayEffect(effectType, position, rotation, false, default, default(NetworkId));
        }

        /// <summary>
        /// ループせずに生成するエフェクト（親オブジェクト指定）
        /// </summary>
        public void RequestPlayOneShotEffect(EffectType effectType, Vector3 position, Quaternion rotation, Transform parent)
        {
            if (effectType == EffectType.None) return;

            NetworkId parentNetworkId = default(NetworkId);
            if (parent != null)
            {
                var parentNetworkObject = parent.GetComponent<NetworkObject>();
                if (parentNetworkObject != null)
                {
                    parentNetworkId = parentNetworkObject.Id;
                }
            }

            RPC_PlayEffect(effectType, position, rotation, false, default, parentNetworkId);
        }

        private EffectID GenerateEffectId()
        {
            return new EffectID(++_effectCount, Runner.LocalPlayer);
        }

        /// <summary>
        /// 手動で削除するエフェクトのリクエスト
        /// </summary>
        public EffectID RequestPlayLoopEffect(EffectType effectType, Vector3 position, Quaternion rotation)
        {
            if (effectType == EffectType.None) return default;

            EffectID effectId = GenerateEffectId();
            RPC_PlayEffect(effectType, position, rotation, true, effectId, default(NetworkId));

            return effectId;
        }

        /// <summary>
        /// 手動で削除するエフェクトのリクエスト（スケール指定）
        /// </summary>
        public EffectID RequestPlayLoopEffect(EffectType effectType, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            if (effectType == EffectType.None) return default;

            EffectID effectId = GenerateEffectId();
            RPC_PlayEffectWithScale(effectType, position, rotation, scale, true, effectId, default(NetworkId));

            return effectId;
        }

        /// <summary>
        /// 手動で削除するエフェクトのリクエスト（親オブジェクト指定）
        /// </summary>
        /// <param name="effectId">ユーザー名＋タイムスタンプ推奨</param>
        public EffectID RequestPlayLoopEffect(EffectType effectType, Vector3 position, Quaternion rotation, Transform parent)
        {
            if (effectType == EffectType.None) return default;

            NetworkId parentNetworkId = default(NetworkId);
            if (parent != null)
            {
                var parentNetworkObject = parent.GetComponentInParent<NetworkObject>();
                if (parentNetworkObject != null)
                {
                    parentNetworkId = parentNetworkObject.Id;
                }
                else
                {
                    Debug.LogWarning($"[EffectSpawner] 指定オブジェクトにNetworkObjectが存在しないため、親オブジェクトを設定できません。parent: {parent}");
                }
            }

            EffectID effectId = GenerateEffectId();
            RPC_PlayEffect(effectType, position, rotation, true, effectId, parentNetworkId);

            return effectId;
        }

        /// <summary>
        /// 指定されたIDのエフェクトを停止する
        /// </summary>
        public void StopEffect(EffectID effectId)
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
        /// <param name="parentNetworkId">親オブジェクトのNetworkID</param>
        [Rpc(RpcSources.All, RpcTargets.All)]
        private void RPC_PlayEffect(EffectType effectType, Vector3 position, Quaternion rotation, bool isLoop, EffectID effectId, NetworkId parentNetworkId)
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

            // 親オブジェクトの取得
            Transform parent = null;
            if (parentNetworkId != default(NetworkId))
            {
                if (_networkRunner.TryFindObject(parentNetworkId, out NetworkObject parentNetworkObject))
                {
                    parent = parentNetworkObject.transform;
                }
                else
                {
                    Debug.LogWarning($"親オブジェクト NetworkID '{parentNetworkId}' が見つかりません");
                }
            }

            GameObject effect;
            if (parent != null)
            {
                // 親オブジェクトが指定されている場合、子オブジェクトとして生成
                effect = Instantiate(effectData.Prefab, position, rotation, parent);
            }
            else
            {
                // 親オブジェクトが指定されていない場合、通常通り生成
                effect = Instantiate(effectData.Prefab, position, rotation);
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
                }
                else
                {
                    // ワンショットエフェクトの場合
                    main.stopAction = ParticleSystemStopAction.Destroy;
                    system.Play();
                }
            }
            else
            {
                Debug.LogWarning($"エフェクト '{effectType}' にParticleSystemが見つかりません");
                
                // ループエフェクトでParticleSystemがない場合も辞書に追加
                if (isLoop && effectId.IsValid)
                {
                    _activeEffects[effectId] = effect;
                }
            }
        }
        
        /// <summary>
        /// スケール付きエフェクト再生RPC
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.All)]
        private void RPC_PlayEffectWithScale(EffectType effectType, Vector3 position, Quaternion rotation, Vector3 scale, bool isLoop, EffectID effectId, NetworkId parentNetworkId)
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

            Transform parent = null;
            if (parentNetworkId != default(NetworkId))
            {
                if (_networkRunner.TryFindObject(parentNetworkId, out NetworkObject parentNetworkObject))
                {
                    parent = parentNetworkObject.transform;
                }
            }

            GameObject effect;
            if (parent != null)
            {
                effect = Instantiate(effectData.Prefab, position, rotation, parent);
            }
            else
            {
                effect = Instantiate(effectData.Prefab, position, rotation);
            }

            effect.transform.localScale = scale;

            ParticleSystem system = effect.GetComponent<ParticleSystem>();
            if (system != null)
            {
                var main = system.main;
                main.loop = isLoop;

                if (isLoop)
                {
                    system.Play();
                    _activeEffects[effectId] = effect;
                }
                else
                {
                    main.stopAction = ParticleSystemStopAction.Destroy;
                    system.Play();
                }
            }
            else
            {
                if (isLoop && effectId.IsValid)
                {
                    _activeEffects[effectId] = effect;
                }
            }
        }

        //エフェクトを止める
        [Rpc(RpcSources.All, RpcTargets.All)]
        private void RPC_StopEffectById(EffectID effectId)
        {
            if (_activeEffects.TryGetValue(effectId, out GameObject effect))
            {
                if (effect != null)
                {
                    Destroy(effect);
                }
                _activeEffects.Remove(effectId);
            }
            else
            {
                Debug.LogWarning($"[EffectSpawner] エフェクトID:{effectId} は存在しません");
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
