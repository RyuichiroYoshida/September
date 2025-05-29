using UnityEngine;
using Fusion;
using System.Collections.Generic;
using System;

namespace September.InGame.Effect
{
    public class EffectManager : NetworkBehaviour
    {
        [SerializeField] private EffectDatabase _effectDatabase;

        private Dictionary<Guid, GameObject> _activeEffects;

        public override void Spawned()
        {
            _activeEffects = new();
        }

        /// <summary>
        /// エフェクトを再生し、そのインスタンスIDを返す
        /// </summary>
        public Guid PlayEffect(Guid guid, Vector3 position, Quaternion rotation)
        {
            Guid id = Guid.NewGuid();

            if (Object.HasInputAuthority)
            {
                //RPC_PlayEffect(guid, position, rotation);
            }

            return id;
        }

        /// <summary>
        /// 指定IDのエフェクトを停止（削除）する
        /// </summary>
        public void StopEffectById(Guid instanceId)
        {
            if (Object.HasInputAuthority)
            {
                RPC_StopEffectById(instanceId);
            }
        }

        //エフェクトの発火
        [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
        private void RPC_PlayEffect(string prefabGuid, Vector3 position, Quaternion rotation, EffectType type)
        {
            var effectData = _effectDatabase.GetEffectData(prefabGuid);

            if (effectData.Prefab == null)
            {
                Debug.LogError($"GUID '{prefabGuid}' に対応するプレハブが見つかりません");
                return;
            }

            GameObject effect = Instantiate(effectData.Prefab, position, rotation);
        }

        //エフェクトを削除
        [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
        private void RPC_StopEffectById(Guid instanceId)
        {
            
        }
    }
}
