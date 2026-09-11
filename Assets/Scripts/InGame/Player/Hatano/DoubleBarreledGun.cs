using System;
using Fusion;
using InGame.Health;
using InGame.Player.Ability.Effect.Shooting;
using InGame.Player.Hatano;
using September.Common;
using UnityEngine;

namespace InGame.Player.Ability
{
    [Serializable]
    public class DoubleBarreledGun : ShootingAbilityBase
    {
        [Header("射撃インターバル")] 
        [SerializeField] private float _shootingInterval;
        private float _shootingIntervalTimer;
        [Header("通常時のダメージ")]
        [SerializeField] private int _damage;
        [Header("鬼の時のダメージ")] 
        [SerializeField] private int _ogreDamage;
        
        private HatanoAbilityStatusManagement _abilityStatusManagement;
        
        protected override void OnStart()
        {
            base.OnStart();
            if(_abilityStatusManagement == null) _abilityStatusManagement = 
                Parameter.Owner.GetComponent<HatanoAbilityStatusManagement>();
            _shootingType = ShootingStateType.Stance;
        }

        protected override void OnUpdate(float deltaTime)
        {
            if(_abilityStatusManagement.AbilityStatus != HatanoAbilityStatus.DoubleBarreledGun) return;
            
            ShootingInputJudgment();
            GunInterval();
            StateDetection();
        }

        /// <summary>
        /// 射撃後のインターバル処理
        /// </summary>
        private void GunInterval()
        {
            //撃つステートの場合、タイマーを加算していく
            if (_shootingType == ShootingStateType.Shooting)
            {
                _shootingIntervalTimer += Runner.DeltaTime;
                //タイマーが時間を超えたら再度、構えステートに変更
                if (_shootingIntervalTimer >= _shootingInterval)
                {
                    _shootingType = ShootingStateType.Stance;
                    _shootingIntervalTimer = 0;
                }
            }
        }

        /// <summary>
        /// 射撃入力を受けたら
        /// 左右のマズルからRayを飛ばして、Hitした場所にRayを飛ばす
        /// </summary>
        private void GunShootingDetection()
        {
            var aimOri = _aimCameraController.AimOrigin;
            var aimDir = _aimCameraController.AimDirection;
            var targetPos = ShootingPositionDetection(aimOri, aimDir);
            
            //左
            var originLeft = _muzzlePos[0].position;
            var dirLeft = targetPos - originLeft;
            Debug.DrawRay(originLeft, dirLeft * _shootingDistance, Color.blue);
            //右
            var originRight = _muzzlePos[1].position;
            var dirRight = targetPos - originLeft;
            Debug.DrawRay(originRight, dirRight * _shootingDistance, Color.blue);
            
            //左右のマズルから、ヒットした場所にRayを飛ばす
            Physics.Raycast(originLeft, dirLeft, out var gunHitInfoLeft, _shootingDistance);
            Physics.Raycast(originRight, dirRight, out var gunHitInfoRight, _shootingDistance);
            GetGunHitPointIDamageable(gunHitInfoLeft, gunHitInfoRight);
        }

        /// <summary>
        /// ヒットしたコライダーからIDamageableを取得
        /// </summary>
        /// <param name="hitLeft">左マズルのヒットした場所</param>
        /// <param name="hitRight">右マズルのヒットした場所</param>
        private void GetGunHitPointIDamageable(RaycastHit hitLeft, RaycastHit hitRight)
        {
            //自身に当たった場合、処理を行わない
            if(hitLeft.collider.GetComponentInParent<NetworkObject>() == Parameter.Owner) return;
            if(hitRight.collider.GetComponentInParent<NetworkObject>() == Parameter.Owner) return;
            var damageableL = hitLeft.collider.GetComponentInParent<IDamageable>();
            var damageableR = hitRight.collider.GetComponentInParent<IDamageable>();
            GunDamage(damageableL);
            GunDamage(damageableR);
        }

        /// <summary>
        /// ダメージを与える処理
        /// </summary>
        /// <param name="damageable">ヒットしたコライダーのIDamageable</param>
        private void GunDamage(IDamageable damageable)
        {
            if(damageable == null) return;
            
            var inputAuthority = Parameter.Owner.InputAuthority;
            //ダメージ処理
            bool enableData = PlayerDatabase.Instance.PlayerDataDic.TryGet(inputAuthority, out var sessionData);
            var hitData = new HitData(HitActionType.RangedDamage,
                enableData && sessionData.IsOgre ? _ogreDamage : _damage, inputAuthority,
                damageable.OwnerPlayerRef, null, damageable);
            damageable.TakeHit(ref hitData);
        }

        protected override void OnShooting()
        {
            GunShootingDetection();
            _shootingType = ShootingStateType.Shooting;
        }
    }
}
