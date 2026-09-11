using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Fusion;
using InGame.Common;
using InGame.Health;
using September.Common;
using September.InGame.UI;
using UnityEngine;

namespace InGame.Player.Sarutobi
{
    public class ThrowKunai : NetworkBehaviour, IAfterTick, IHitExecutor
    {
        [SerializeField] private float _cooldown;
        [SerializeField] private int _damage;
        [SerializeField] private int _ogreDamage;
        [SerializeField] private float _maxDistance;
        [SerializeField] private LayerMask _hitLayer;
        [SerializeField] private Transform _muzzleTf;
        [SerializeField] private Vector3 _stanceCameraOffset;
        [SerializeField] private float _changeOffsetDuration;
        [SerializeField] private int _defaultBulletCount = 1;
        [SerializeField] private float _angleBetweenBullets = 2.5f;
        [Header("クナイホーミング範囲")]
        [SerializeField] private Vector2 _homingRange;
        [Header("AnimationClip")]
        [SerializeField] private AnimationClip _stance;
        [SerializeField] private AnimationClip _stanceLoop;
        [SerializeField] private AnimationClip _throw;
        [Header("Particle")]
        [SerializeField] private ParticleSystem _kunaiParticle;
        [SerializeField] private float _kunaiSpeed;
        [SerializeField] private ParticleSystem _bulletMark;
        [Header("UI")]
        [SerializeField] private GameObject _crosshairPrefab;
        [Header("他プレイヤー")]
        [SerializeField] private List<GameObject> _otherPlayers;

        private Camera _mainCamera;
        private PlayerManager _playerManager;
        private AnimationClipPlayer _clipPlayer;
        private PlayerMovement _movement;
        private CameraController _cameraController;
        private AbilityGrapplingHook _grapplingHook;
        private GameObject _crosshair;
        private float _cooldownTimer;
        private NetworkButtons PreviousButtons { get; set; }

        private bool CanThrow => _cooldownTimer <= 0 && State == KunaiStateType.Ready && _grapplingHook &&
                                 _grapplingHook.AbilityState != AbilityGrapplingHook.AbilityStateType.Active;

        [Networked] private int BulletCountNetwork { get; set; }
        [Networked, HideInInspector] public KunaiStateType State { get; private set; } = KunaiStateType.Idol;

        public override void Spawned()
        {
            if (HasStateAuthority)
            {
                _playerManager = GetComponent<PlayerManager>();
                _movement = GetComponent<PlayerMovement>();
                _clipPlayer = GetComponent<AnimationClipPlayer>();
                _grapplingHook = GetComponent<AbilityGrapplingHook>();
                _grapplingHook.OnAbilityStart += EndStance;
                BulletCountNetwork = _defaultBulletCount;
            }

            if (HasInputAuthority)
            {
                _mainCamera = Camera.main;
                _playerManager = GetComponent<PlayerManager>();
                _movement = GetComponent<PlayerMovement>();
                _cameraController = GetComponent<CameraController>();
                if (!_grapplingHook) _grapplingHook = GetComponent<AbilityGrapplingHook>();
                _crosshair = Instantiate(_crosshairPrefab);
                _crosshair.SetActive(false);

                //全プレイヤーを取得
                GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
                if (players.Length > 0)
                {
                    //自身以外のプレイヤーを保持（クナイホーミングのため）
                    foreach (var p in players)
                    {
                        if (p == gameObject) continue;
                        _otherPlayers.Add(p);
                    }
                }
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (HasStateAuthority && _grapplingHook)
            {
                _grapplingHook.OnAbilityStart -= EndStance;
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!GetInput<PlayerInput>(out var input)) return;

            if (State == KunaiStateType.Idol &&
                _playerManager.CurrentPlayerControlState == PlayerManager.PlayerControlState.Normal &&
                input.Buttons.WasPressed(PreviousButtons, PlayerButtons.Ability2) &&
                _grapplingHook && _grapplingHook.AbilityState != AbilityGrapplingHook.AbilityStateType.Active)
            {
                StartStance().Forget();
            }

            if (State != KunaiStateType.Idol && input.Buttons.WasReleased(PreviousButtons, PlayerButtons.Ability2))
            {
                EndStance();
            }
            else if (input.Buttons.WasPressed(PreviousButtons, PlayerButtons.Attack) && CanThrow)
            {
                PlayCharacterThrowAnimation().Forget();

                if (HasInputAuthority)
                {
                    var targetPlayer = AcquireAttackTarget();
                    for (int i = 0; i < BulletCountNetwork; i++)
                    {
                        // 扇状にクナイを発射する
                        // iを中央基準に変換（ 0,1,2,3,4 を -2,-1,0,1,2 に変換）した後、クナイ間の角度をかける
                        var angle = (i - (BulletCountNetwork - 1) / 2f) * _angleBetweenBullets;
                        Throw(angle, targetPlayer);
                    }
                }
            }

            if (State == KunaiStateType.Ready || State == KunaiStateType.Stance)
            {
                _movement.SetRotationDirection(HasInputAuthority ? _mainCamera.transform.forward : input.DesiredLookDirection);
            }

            SetCooldownTimer();
        }

        public void AfterTick()
        {
            PreviousButtons = GetInput<PlayerInput>().GetValueOrDefault().Buttons;
        }

        async UniTask StartStance()
        {
            if (HasInputAuthority)
            {
                _cameraController.ChangeOffset(_stanceCameraOffset, _changeOffsetDuration);
                _crosshair.SetActive(true);
            }

            _playerManager.SetControlState(PlayerManager.PlayerControlState.InputLocked);

            if (!HasStateAuthority) return;

            RPC_ChangeDescriptionUI(ControlDescriptionType.SarutobiAiming);
            State = KunaiStateType.Stance;
            var endType = await _clipPlayer.PlayClipAndWait(_stance);

            if (endType == EndClipType.Complete)
            {
                State = KunaiStateType.Ready;
                _clipPlayer.PlayClip(_stanceLoop);
            }
            else
            {
                // 中断された場合は明示的にクリップを停止してからEndStanceを呼ぶ
                _clipPlayer.StopClip(_stance);
                EndStance();
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
        private void RPC_ChangeDescriptionUI(ControlDescriptionType mode)
        {
            UIController.I.ChangeDescriptionUI(mode);
        }

        /// <summary>
        /// クナイの投げる数を設定する（仮）
        /// ステータスシステムに統合したい 
        /// </summary>
        public void SetBulletCount(int bulletCount)
        {
            if (!HasStateAuthority) return;
            BulletCountNetwork = bulletCount;
        }

        void Throw(float angle, GameObject nearPlayer = null)
        {
            if (!HasInputAuthority) return;

            Vector3 dir;
            // 画面内で一番近いプレイヤーがいる場合、そのプレイヤーの方向を使用する
            if (nearPlayer != null)
            {
                dir = (nearPlayer.transform.position - _mainCamera.transform.position).normalized;
            }
            else // カメラの前方方向にangle分回転させた方向を計算
            {
                var rot = Quaternion.AngleAxis(angle, Vector3.up);
                dir = rot * _mainCamera.transform.forward;
            }

            var hit = Physics.Raycast(_mainCamera.transform.position, dir, out var hitInfo, _maxDistance, _hitLayer);
            var target = hit
                ? hitInfo.point
                : _mainCamera.transform.position + dir * _maxDistance;
            Throw(target);
        }

        /// <summary>
        /// 画面中央に一番近いプレイヤーを取得する
        /// </summary>
        /// <returns>画面中心に最も近いプレイヤー</returns>
        private GameObject AcquireAttackTarget()
        {
            if (_otherPlayers.Count == 0) return null;

            GameObject nearPlayer = null; // 最も近いプレイヤーを保持
            float mathf = Mathf.Infinity; //距離比較用
            Vector2 center = new Vector2(Screen.width / 2f, Screen.height / 2f); //画面中央

            // 画面中央から一番近いプレイヤーを判定
            foreach (var p in _otherPlayers)
            {
                // ワールド座標をカメラのスクリーン座標へと変換
                var screenPoint = _mainCamera.WorldToScreenPoint(p.transform.position);
                // カメラに映っていて、範囲内にいるかを判定する
                bool isScreen = screenPoint.x >= 0 && screenPoint.x <= Screen.width &&
                              screenPoint.y >= 0 && screenPoint.y <= Screen.height && screenPoint.z > 0;
                // 画面中央からプレイヤーの距離がホーミング範囲内かを判定する
                bool isRange = Mathf.Abs(screenPoint.x - center.x) <= _homingRange.x &&
                               Mathf.Abs(screenPoint.y - center.y) <= _homingRange.y;
                bool isView = isScreen && isRange;
                if (!isView) continue;

                // 画面中央からプレイヤーの距離を計算
                float distance = Vector2.Distance(new Vector2(screenPoint.x, screenPoint.y), center);
                if (distance < mathf) // 最も近いプレイヤーを更新していく
                {
                    nearPlayer = p;
                    mathf = distance;
                }
            }

            return nearPlayer;
        }

        void Throw(Vector3 targetPos)
        {
            if (HasInputAuthority)
            {
                KunaiTargetDetection(targetPos);
            }
        }

        async UniTask PlayCharacterThrowAnimation()
        {
            if (!HasStateAuthority) return;
            State = KunaiStateType.Stance;
            var endType = await _clipPlayer.PlayClipAndWait(_throw);

            if (endType == EndClipType.Complete)
            {
                StartStance().Forget();
            }
            else
            {
                // 投げモーションが中断された場合も明示的にクリップを停止してからEndStanceを呼ぶ
                _clipPlayer.StopClip(_throw);
                EndStance();
            }
        }

        void EndStance()
        {
            if (HasInputAuthority)
            {
                _cameraController.ResetOffset(_changeOffsetDuration);
                _crosshair.SetActive(false);
            }
            else
            {
                Rpc_EndStance();
            }
            if (HasStateAuthority)
            {
                RPC_ChangeDescriptionUI(ControlDescriptionType.Sarutobi);
            }
            _playerManager.SetControlState(PlayerManager.PlayerControlState.Normal);
            State = KunaiStateType.Idol;
            // より確実にすべての関連アニメーションクリップを停止
            if (_clipPlayer)
            {
                _clipPlayer.StopClip(_stanceLoop);
                _clipPlayer.StopClip(_stance);
                _clipPlayer.StopClip(_throw);
            }
        }

        // state authority から強制終了したときにLocalに反映する
        [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
        void Rpc_EndStance()
        {
            _cameraController.ResetOffset(_changeOffsetDuration);
            _crosshair.SetActive(false);
        }

        // Local で投げる位置の判定をとる
        void KunaiTargetDetection(Vector3 targetPos)
        {
            if (!HasInputAuthority) return;
            Rpc_KunaiBulletDetection(targetPos);
        }

        /// <summary> input authority から target position を取得し、クナイの判定をとる </summary>
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        void Rpc_KunaiBulletDetection(Vector3 targetPos)
        {
            var muzzleHit = Physics.Raycast(_muzzleTf.position, targetPos - _muzzleTf.position, out var muzzleHitInfo, _maxDistance, _hitLayer, QueryTriggerInteraction.Ignore);

            if (muzzleHit)
            {
                var damageable = muzzleHitInfo.collider.GetComponentInParent<IDamageable>();

                if (damageable != null)
                {
                    // ダメージ処理
                    bool enableData = PlayerDatabase.Instance.PlayerDataDic.TryGet(Object.InputAuthority, out var sessionData);
                    var hitData = new HitData(HitActionType.RangedDamage,
                        enableData ? sessionData.IsOgre ? _ogreDamage : _damage : _damage, Object.InputAuthority,
                        damageable.OwnerPlayerRef, this);
                    damageable.TakeHit(ref hitData);
                }
            }

            // 各クライアントでParticleの再生
            RPC_PlayKunaiThrowParticle(muzzleHit ? muzzleHitInfo.point : targetPos,
                muzzleHit ? muzzleHitInfo.distance : Vector3.Distance(_muzzleTf.transform.position, targetPos),
                muzzleHitInfo.normal, muzzleHit);
        }

        [Rpc]
        void RPC_PlayKunaiThrowParticle(Vector3 hitPos, float hitDistance, Vector3 hitNormal, bool isHit)
        {
            float duration = hitDistance / _kunaiSpeed;
            ParticlePool.Play(_kunaiParticle, _muzzleTf.position, Quaternion.LookRotation(hitPos - _muzzleTf.position), duration + 2f)
                .transform.DOMove(hitPos, duration);
            if (isHit) ParticlePool.Play(_bulletMark, hitPos, Quaternion.LookRotation(hitNormal));
        }

        void SetCooldownTimer()
        {
            if (_cooldownTimer > 0) _cooldownTimer -= Runner.DeltaTime;
        }

        public void HitExecution(HitData hitData)
        {
            throw new System.NotImplementedException();
        }

        public enum KunaiStateType
        {
            Idol,
            Stance,
            Ready
        }
    }
}
