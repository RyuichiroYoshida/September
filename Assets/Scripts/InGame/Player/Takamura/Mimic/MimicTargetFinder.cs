using Fusion;
using September.Common;
using UnityEngine;

namespace InGame.Player.Takamura.Mimic
{
    /// <summary>
    /// プレイヤーの視線と現在のPlayerDatabaseから擬態対象を選択する。
    /// 判定はStateAuthorityで実行し、クライアントから送られたカメラ情報を使用する。
    /// </summary>
    public static class MimicTargetFinder
    {
        /// <summary>
        /// 擬態対象のキャラクターを探すメソッド
        /// </summary>
        /// <param name="owner">プレイヤー</param>
        /// <param name="input">入力</param>
        /// <param name="maxDistance">判定距離</param>
        /// <param name="maxAngle">判定角度</param>
        /// <param name="lineOfSightMask">判定対象のLayer</param>
        /// <param name="target">擬態対象のオブジェクト</param>
        /// <returns>対象を見つけられたかどうか</returns>
        public static bool TryFindTarget(
            NetworkObject owner,
            in PlayerInput input,
            float maxDistance,
            float maxAngle,
            LayerMask lineOfSightMask,
            out NetworkObject target,
            out CharacterType targetCharacterType)
        {
            target = null;
            targetCharacterType = CharacterType.None;

            // 必要な参照がなければ終了
            if (!owner || PlayerDatabase.Instance == null)
                return false;

            // 入力からカメラの座標や向いている方向を計算
            var cameraPosition = input.CameraPosition;
            var lookDirection = input.DesiredLookDirection.normalized;
            if (lookDirection.sqrMagnitude <= Mathf.Epsilon)
                lookDirection = owner.transform.forward;

            var bestAngle = float.MaxValue;

            // すべてのプレイヤーの情報に対して計算
            foreach (var pair in PlayerDatabase.Instance.PlayerObjectDic)
            {
                var candidate = pair.Value;
                // 他プレイヤーの情報でなければ飛ばす
                if (!candidate || candidate == owner)
                    continue;

                if (!PlayerDatabase.Instance.PlayerDataDic.TryGet(
                pair.Key,
                out var playerData))
                {
                    continue;
                }

                CharacterType candidateType =
                    playerData.CharacterType;

                // キャラクターではない予約値だけ除外
                if (candidateType == CharacterType.None ||
                    candidateType == CharacterType.All)
                {
                    continue;
                }

                // 対象の座標を計算
                var targetPosition = GetTargetPosition(candidate);
                // 方向を計算
                var direction = targetPosition - cameraPosition;
                // 距離を計算
                var distance = direction.magnitude;
                if (distance <= Mathf.Epsilon || distance > maxDistance)
                    continue;

                // 角度を計算
                var angle = Vector3.Angle(lookDirection, direction);
                if (angle > maxAngle || angle >= bestAngle)
                    continue;

                // Rayを飛ばして対象に当たらなければ飛ばす
                if (!HasLineOfSight(candidate, cameraPosition, targetPosition, lineOfSightMask))
                    continue;

                bestAngle = angle;
                target = candidate;
                targetCharacterType = candidateType;
            }

            return target;
        }

        /// <summary>
        /// オブジェクトの座標を計算するメソッド
        /// </summary>
        /// <param name="target">座標を計算したいオブジェクト</param>
        /// <returns>オブジェクトの座標</returns>
        private static Vector3 GetTargetPosition(NetworkObject target)
        {
            var targetCollider = target.GetComponentInChildren<Collider>();
            return targetCollider
                ? targetCollider.bounds.center
                : target.transform.position + Vector3.up;
        }

        /// <summary>
        /// Rayを飛ばして対象に当たるかどうかを判定するメソッド
        /// </summary>
        /// <param name="target">ターゲットのオブジェクト</param>
        /// <param name="origin">Rayの始点</param>
        /// <param name="targetPosition">ターゲットの座標</param>
        /// <param name="lineOfSightMask">対象のLayer</param>
        /// <returns>Rayが対象に当たったかどうか</returns>
        private static bool HasLineOfSight(
            NetworkObject target,
            Vector3 origin,
            Vector3 targetPosition,
            LayerMask lineOfSightMask)
        {
            var direction = targetPosition - origin;
            var distance = direction.magnitude;

            if (!Physics.Raycast(
                    origin,
                    direction.normalized,
                    out var hit,
                    distance,
                    lineOfSightMask,
                    QueryTriggerInteraction.Collide))
            {
                return false;
            }

            // 当たったオブジェクトがターゲットと一致したか
            return hit.collider.GetComponentInParent<NetworkObject>() == target;
        }
    }
}
