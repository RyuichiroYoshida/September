using Fusion;
using UnityEngine;

namespace InGame.Player.Hatano
{
    /// <summary>
    /// 武器のソケットを変更する
    /// </summary>
    public class HatanoWeaponController : NetworkBehaviour
    {
        [Header("ソケット（ロケット）")]
        [SerializeField] private Transform _rocketSocketRoot;
        [SerializeField] private Transform _rocketSocketBody;
        [SerializeField] private Transform _rocketSocketHand;
        [Header("ソケット（二丁拳銃）")]
        [SerializeField] private Transform[] _doubleSocketBody;
        [SerializeField] private Transform[] _doubleSocketHand;
        [Header("ソケット（レーザー銃）")]
        [SerializeField] private Transform _laserSocketHip;
        [SerializeField] private Transform _laserSocketHand;
        [Header("武器（Prefab）")]
        [SerializeField] private Transform _rocketPrefabTransform;
        [SerializeField] private Transform[] _doublePrefabTransform;
        [SerializeField] private Transform _laserPrefabTransform;
        
        #region 二丁拳銃

        public void AttachDoubleGunBody()
        {
            AttachSocket(_doublePrefabTransform[0], _doubleSocketBody[0]);
            AttachSocket(_doublePrefabTransform[1], _doubleSocketBody[1]);
        }

        public void AttachDoubleGunHand()
        {
            AttachSocket(_doublePrefabTransform[0], _doubleSocketHand[0]);
            AttachSocket(_doublePrefabTransform[1], _doubleSocketHand[1]);
        }

        #endregion
        
        #region レーザー銃

        public void AttachLaserGunHip()
        {
            AttachSocket(_laserPrefabTransform, _laserSocketHip);
        }

        public void AttachLaserGunHand()
        {
            AttachSocket(_laserPrefabTransform, _laserSocketHand);
        }

        #endregion
        
        #region ロケットランチャー

        public void AttachRocketRoot()
        {
            AttachSocket(_rocketPrefabTransform, _rocketSocketRoot);
        }

        public void AttachRocketBody()
        {
            AttachSocket(_rocketPrefabTransform, _rocketSocketBody);
        }

        public void AttachRocketSocketHand()
        {
            AttachSocket(_rocketPrefabTransform, _rocketSocketHand);
        }

        #endregion

        /// <summary>
        /// ULT終了後の武器ソケット変更
        /// </summary>
        /// <param name="status">現在のアビリティ</param>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_UltEndAttachSocket(HatanoAbilityStatus status)
        {
            AttachRocketBody();
            if (status == HatanoAbilityStatus.DoubleBarreledGun)
            {
                AttachDoubleGunHand();
                AttachLaserGunHip();
            }
            else // レーザー銃
            {
                AttachLaserGunHand();
                AttachDoubleGunBody();
            }
        }
        
        private void AttachSocket(Transform prefab, Transform socket)
        {
            prefab.SetParent(socket);
            
            prefab.localPosition = Vector3.zero;
            prefab.localRotation = Quaternion.identity;
            prefab.localScale = Vector3.one;
        }
    }
}
