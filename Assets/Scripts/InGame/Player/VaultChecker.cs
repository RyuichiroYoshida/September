using UnityEngine;

namespace InGame.Player
{
    public static class VaultChecker
    {
        public static bool TryVault(VaultParameter parameter, out VaultResult result, out VaultFailReason reason)
        {
            reason = VaultFailReason.None;

            result = new();
            result.vaultStart = Vector3.zero;
            result.vaultTop = Vector3.zero;
            result.vaultEnd = Vector3.zero;

            Vector3 moveDirection = parameter.moveDirection;
            Vector3 position = parameter.Position;

            Vector3 moveDir3 = new Vector3(moveDirection.x, 0, moveDirection.z).normalized;
            Debug.DrawLine(position, position + moveDir3, Color.red);

            // ステップ1：キャラクターが歩くことができない壁やオブジェクトを見つけるために前方にCastします。
            const float castStartBackOffset = 0.05f;
            Vector3 castStartOffset = -moveDir3 * castStartBackOffset;
            float frontCastDistance = parameter.reachDistance + castStartBackOffset;
            Vector3 point1 = position + Vector3.up * (parameter.maxLedgeHeight - parameter.capsuleRadius) + castStartOffset;
            Vector3 point2 = position + Vector3.up * (parameter.minLedgeHeight + parameter.capsuleRadius) + castStartOffset;
            Debug.DrawLine(point1, point2, Color.red, 1f);
            if (!Physics.CapsuleCast(point1, point2, parameter.capsuleRadius + 0.01f, moveDir3,
                out var frontHitInfo, frontCastDistance, ~0))
            {
                reason = VaultFailReason.NoFrontHit;
                return false;
            }

            float frontHitDistance = Mathf.Max(0f, frontHitInfo.distance - castStartBackOffset);

            // hit point が歩けるかどうか
            bool walkable = Vector3.Angle(Vector3.up, frontHitInfo.normal) <= parameter.groundSlopeThreshold;
            if (walkable)
            {
                reason = VaultFailReason.FrontIsWalkable;
                return false;
            }

            // ステップ2：乗り越えることのできる高さであるかの判定
            Vector3 origin = frontHitInfo.point - frontHitInfo.normal * 0.3f;
            origin.y = position.y + parameter.maxLedgeHeight + parameter.capsuleRadius;

            if (!Physics.SphereCast(origin, parameter.capsuleRadius, Vector3.down,
                out var heightHitInfo, parameter.maxLedgeHeight - parameter.minLedgeHeight, parameter.groundLayer))
            {
                reason = VaultFailReason.NoHeightHit;
                return false;
            }

            float ledgeHeight = heightHitInfo.point.y - position.y;

            if (ledgeHeight > parameter.maxLedgeHeight || ledgeHeight < parameter.minLedgeHeight)
            {
                reason = VaultFailReason.HeightOutOfRange;
                return false;
            }
            Vector3 pos1 = heightHitInfo.point + heightHitInfo.normal * (parameter.capsuleRadius - 0.02f);

            Vector3 pos2 = heightHitInfo.point + Vector3.up * (parameter.capsuleHeight - parameter.capsuleRadius);

            Collider[] overlaps = Physics.OverlapCapsule(pos1, pos2, parameter.capsuleRadius + 0.01f, parameter.groundLayer);

            if (overlaps.Length > 0)
            {
                bool isHit = false;
                foreach (var col in overlaps)
                {
                    if (col.gameObject == frontHitInfo.collider.gameObject) continue;
                    isHit = true;
                    Debug.Log(col.name);
                }
                if (isHit)
                {
                    reason = VaultFailReason.NoHeadSpace;
                    return false;
                }
            }

            // ステップ3：乗り越えられる障害物の奥行とスペースがあるかの判定
            float halfHeight = parameter.capsuleHeight * 0.5f;

            Vector3 p1 = frontHitInfo.point;
            p1.y = position.y + parameter.capsuleRadius + parameter.capsuleHeight;

            Vector3 p2 = frontHitInfo.point;
            p2.y = position.y + parameter.capsuleRadius;

            if (Physics.CapsuleCast(p1, p2, parameter.capsuleRadius,
                -frontHitInfo.normal,
                out var secondHit,
                parameter.maxLedgeDepth + frontHitDistance,
                parameter.groundLayer))
            {
                reason = VaultFailReason.BlockedForward;
                return false;
            }

            Vector3 reverseOrigin = p2 + Vector3.up * halfHeight
                - frontHitInfo.normal * (parameter.maxLedgeDepth + frontHitDistance);

            // 逆向きにCastして、障害物の奥行を求める
            if (!Physics.CapsuleCast(
                reverseOrigin + Vector3.up * halfHeight,
                reverseOrigin + Vector3.down * halfHeight,
                parameter.capsuleRadius,
                frontHitInfo.normal,
                out var backHit,
                parameter.maxLedgeDepth + frontHitDistance,
                parameter.groundLayer))
            {
                reason = VaultFailReason.NoBackHit;
                return false;
            }

            // 奥行がありすぎたら終了
            if (backHit.distance < frontHitDistance)
            {
                reason = VaultFailReason.BackTooClose;
                return false;
            }

            result.vaultEnd =
                reverseOrigin
                - frontHitInfo.normal * frontHitDistance
                + Vector3.down * (halfHeight + parameter.capsuleRadius);

            // 最終地点にCheckを入れる
            if (Physics.CheckCapsule(
                result.vaultEnd + Vector3.up * parameter.capsuleRadius,
                result.vaultEnd + Vector3.up * (parameter.capsuleRadius + parameter.capsuleHeight),
                parameter.capsuleRadius - 0.01f,
                parameter.groundLayer))
            {
                reason = VaultFailReason.EndBlocked;
                return false;
            }

            result.vaultStart = position;
            result.vaultTop = (frontHitInfo.point + backHit.point) * 0.5f;
            result.vaultTop.y = heightHitInfo.point.y;

            return true;
        }
    }
    public struct VaultParameter
    {
        public Vector3 moveDirection;
        public Vector3 Position;
        public float capsuleRadius;
        public float capsuleHeight;
        public float reachDistance;
        public float maxLedgeHeight;
        public float minLedgeHeight;
        public float maxLedgeDepth;
        public float groundSlopeThreshold;
        public LayerMask groundLayer;
    }
    public ref struct VaultResult
    {
        public Vector3 vaultStart;
        public Vector3 vaultTop;
        public Vector3 vaultEnd;

    }
    public struct CapsuleCastData
    {
        public Vector3 P1;
        public Vector3 P2;
        public float Radius;
        public Vector3 Direction;
        public Vector3 Distance;
        public bool IsHit;
        public RaycastHit HitInfo;
    }
    public enum VaultFailReason
    {
        None,
        NoFrontHit,
        FrontIsWalkable,
        NoHeightHit,
        HeightOutOfRange,
        NoHeadSpace,
        BlockedForward,
        NoBackHit,
        BackTooClose,
        EndBlocked
    }
}
