using UnityEngine;
using System.Collections.Generic;

namespace September.InGame.Common
{
    public class DebugDrawHelper : MonoBehaviour
    {
        private struct SphereDrawInfo
        {
            public Vector3 Position;
            public float Radius;
            public Color Color;
            public float ExpireTime; // 表示終了時間（Time.timeで判定）
        }

        private static readonly List<SphereDrawInfo> _drawQueue = new();

        /// <summary>
        /// 指定位置に指定時間だけワイヤー球を描画する
        /// </summary>
        public static void RegisterAttackPosition(Vector3 pos, float radius, Color color, float durationSeconds = 1f)
        {
            _drawQueue.Add(new SphereDrawInfo
            {
                Position = pos,
                Radius = radius,
                Color = color,
                ExpireTime = Time.time + durationSeconds
            });
        }

        private void Start()
        {
#if !UNITY_EDITOR
            gameObject.SetActive(false); // 本番では無効化
#endif
        }

        private void Update()
        {
            float now = Time.time;
            _drawQueue.RemoveAll(info => now > info.ExpireTime);
        }

        private void OnDrawGizmos()
        {
            foreach (var info in _drawQueue)
            {
                Gizmos.color = info.Color;
                Gizmos.DrawWireSphere(info.Position, info.Radius);
            }
        }
    }
}