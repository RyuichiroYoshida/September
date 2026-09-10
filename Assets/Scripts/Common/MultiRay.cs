using System;
using System.Collections.Generic;
using UnityEngine;

namespace September.Common
{
    [Serializable]
    public struct MultiRay
    {
        public Vector3 StartOrigin;
        public Vector3 EndOrigin;
        public Vector3 Direction;
        public float Distance;
        public int DivideCount;

        /// <summary>
        /// 最初にヒットした衝突情報を返します
        /// </summary>
        public bool RaycastFirst(Vector3 position, Quaternion rotation, LayerMask layerMask, out RaycastHit hit)
        {
            Vector3 forwardRayOrigin = position + rotation * StartOrigin;
            Vector3 backRayOrigin = position + rotation * EndOrigin;

            // 地面判定
            for (int i = 0; i < DivideCount + 2; i++)
            {
                Vector3 rayOrigin = Vector3.Lerp(forwardRayOrigin, backRayOrigin, i / (DivideCount + 1f));

                bool isHit = Physics.Raycast(rayOrigin, rotation * Direction * Distance, out RaycastHit groundHit, Distance, layerMask);
                if (!isHit) continue;

                hit = groundHit;
                return true;
            }

            hit = default;
            return false;
        }

        /// <summary>
        /// 全てのヒットした衝突情報を返します
        /// </summary>
        public IEnumerable<RaycastHit> RaycastAll(Vector3 position, Quaternion rotation, LayerMask layerMask)
        {
            Vector3 forwardRayOrigin = position + rotation * StartOrigin;
            Vector3 backRayOrigin = position + rotation * EndOrigin;

            // 地面判定
            for (int i = 0; i < DivideCount + 2; i++)
            {
                Vector3 rayOrigin = Vector3.Lerp(forwardRayOrigin, backRayOrigin, i / (DivideCount + 1f));

                bool isHit = Physics.Raycast(rayOrigin, rotation * Direction * Distance, out RaycastHit groundHit, Distance, layerMask);
                if (!isHit) continue;

                yield return groundHit;
            }
        }
    }
}
