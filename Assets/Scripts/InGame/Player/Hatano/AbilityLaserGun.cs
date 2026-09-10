using System;
using InGame.Interact;
using InGame.Player.Hatano;
using InGame.Player.Ability.Effect.Shooting;
using UnityEngine;

namespace InGame.Player.Ability
{
    [Serializable]
    public class AbilityLaserGun : ShootingAbilityBase
    {
        [Header("PlayerInteractionController")]
        [SerializeField] private PlayerInteractionController _playerInteractionController;
        [Space(30)]
        [Header("使用時のインタラクション必要時間")] 
        [SerializeField] private float _interactionTime;
        private float _interactionTimer;
        [Header("判定を取るためのBoxの大きさ")]
        [SerializeField] private Vector3 _judgmentBoxSize;
        
        /// <summary>
        /// 現在、インタラクション中のオブジェクトを保持
        /// </summary>
        private InteractableBase _currentInteractableBase;
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
            //現在のAbilityがレーザー銃でない場合、処理をしない
            if (_abilityStatusManagement.AbilityStatus != HatanoAbilityStatus.LaserGun) return;
            
            ShootingInputJudgment();
            StateDetection();
        }

        protected override void OnShooting()
        {
            var camOri = _aimCameraController.AimOrigin;
            var camDir = _aimCameraController.AimDirection;
            var targetPos = ShootingPositionDetection(camOri, camDir);
            var origin = _muzzlePos[0].position; //銃口
            var dir = targetPos - origin;
            Debug.DrawRay(origin, dir * _shootingDistance, Color.blue);
            
            //hitした場所に向かってRayを飛ばす
            var laserPoint = Physics.Raycast(origin, dir, out var laserHitInfo, _shootingDistance);
            if (laserPoint)
            {
                //Rayが当たった場所のColliderを取得して、小さいインタラクションオブジェクトも取得出来るようにする
                Collider[] boxColliders = Physics.OverlapBox(
                    laserHitInfo.point,
                    _judgmentBoxSize,
                    Quaternion.identity);
                
                //インタラクションオブジェクトを取得する
                InteractableBase interactableBase = null;
                foreach (var collider in boxColliders)
                {
                    var obj = collider.gameObject;
                    interactableBase = obj.GetComponentInParent<InteractableBase>()
                                       ?? obj.GetComponent<InteractableBase>()
                                       ?? obj.GetComponentInChildren<InteractableBase>();
                    //一番最初にインタラクションオブジェクトを見つけ次第、ループを抜ける
                    if(interactableBase != null) break;
                }
                
                //インタラクションオブジェクトを取得出来た場合、インタラクションを行う
                if (interactableBase != null)
                {
                    //現在のインタラクションオブジェクトが前回と違う場合、タイマーを初期化
                    if (_currentInteractableBase != interactableBase)
                    {
                        _interactionTimer = 0;
                        _currentInteractableBase = interactableBase;
                    }
                    
                    _playerInteractionController.RemoteInteraction(
                        ref _interactionTimer, _interactionTime, interactableBase, ref _phase, _aimCameraController);
                }
                else //インタラクションを中止する
                {
                    _currentInteractableBase = null;
                    _playerInteractionController.RemoteInteractionCancel(ref _interactionTimer);
                }
            }
            else //hitした場所がなかった場合もタイマーを初期化する
            {
                _currentInteractableBase = null;
                _playerInteractionController.RemoteInteractionCancel(ref _interactionTimer);
            }
        }

        protected override void OnNoShooting()
        {
            _currentInteractableBase = null;
            //遠距離インタラクションを終了する
            _playerInteractionController.RemoteInteractionCancel(ref _interactionTimer);
        }

        protected override void OnStopTheStance()
        {
            _currentInteractableBase = null;
            //遠距離インタラクションを終了する
            _playerInteractionController.RemoteInteractionCancel(ref _interactionTimer);
        }
    }
}
