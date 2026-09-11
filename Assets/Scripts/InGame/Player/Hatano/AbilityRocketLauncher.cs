using System;
using InGame.Health;
using InGame.Player.Ability.Effect.Shooting;
using InGame.Player.Hatano;
using September.Common;
using UnityEngine;

namespace InGame.Player.Ability
{
    [Serializable]
    public class AbilityRocketLauncher : ShootingAbilityBase
    {
        [Header("ロケットランチャーの攻撃範囲")]
        [SerializeField] private float _rocketLauncherRadius;
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
            if(_abilityStatusManagement.AbilityStatus != HatanoAbilityStatus.RocketLauncher) return;
            
            ShootingInputJudgment();
            StateDetection();
        }

        /// <summary>
        /// ロケットランチャーを発射する
        /// </summary>
        private void LauncherShootingDetection()
        {
            var aimOri = _aimCameraController.AimOrigin;
            var aimDir = _aimCameraController.AimDirection;
            var targetPos = ShootingPositionDetection(aimOri, aimDir);
            var origin = _muzzlePos[0].position;
            var dir = targetPos - origin;
            Debug.DrawRay(origin, dir * _shootingDistance, Color.blue);
            
            //hitした場所に向かってRayを飛ばす（プレイヤー（マズル位置）からのRay）
            var laserPoint = Physics.Raycast(origin, dir, out var laserHitInfo, _shootingDistance);
            //ヒットしたところにロケットランチャーを発射
            RocketLauncherRadius(laserHitInfo.point);
        }

        /// <summary>
        /// ロケットランチャーの攻撃処理
        /// </summary>
        /// <param name="targetPos">ヒットした場所</param>
        private void RocketLauncherRadius(Vector3 targetPos)
        {
            //攻撃範囲内のオブジェクトを取得
            Collider[] radiusObjs = Physics.OverlapSphere(targetPos, _rocketLauncherRadius);
            var playerInput = Parameter.Owner.InputAuthority;
            foreach (var obj in radiusObjs)
            {
                var damageable = obj.GetComponentInParent<IDamageable>();
                if(damageable == null) continue;
                
                //自身に当たっていたらスキップ
                if(damageable.OwnerPlayerRef == playerInput) continue;
                
                //ダメージ処理
                bool enableData = PlayerDatabase.Instance.PlayerDataDic.TryGet(playerInput, out var sessionData);
                var damage = _damage;
                if(enableData && sessionData.IsOgre) damage = _ogreDamage; //鬼の場合、鬼時のダメージに変更
                var hitData = new HitData(HitActionType.RangedDamage,
                    damage, playerInput, damageable.OwnerPlayerRef, null, damageable);
                damageable.TakeHit(ref hitData);
            }
        }

        protected override void OnShooting()
        {
            LauncherShootingDetection();
            _phase = AbilityPhase.Ending;
        }

        protected override void OnEndAbility()
        {
            base.OnEndAbility();
            //構え前の状態へ戻す
            _shootingType = ShootingStateType.None;
            _lastShootingType = ShootingStateType.None;
            _aimCameraController.RPC_NormalCamera();
            _aimCameraController.RPC_CrosshairToggleChange(false);
        }
    }
}
