using Common.Extensions;
using Cysharp.Threading.Tasks;
using Fusion;
using InGame.Health;
using InGame.Interact;
using InGame.Player;
using September.Common;
using September.Common.Input;
using September.InGame.Kraken.Animations;
using September.InGame.Kraken.Attack;
using September.InGame.Mountable;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Pool;
using UnityEngine.Timeline;

namespace September.InGame.Kraken
{
    public class Kraken : NetworkBehaviour, IMountable, IDamageable
    {
        /// <summary> クラーケン中のカメラ優先度 </summary>
        private const int CameraPriority = 15;

        /// <summary> クラーケン時カメラのコントローラー </summary>
        [Header("カメラ")]
        [SerializeField] private CameraController _cameraController;

        [Header("攻撃予測設定")]
        [SerializeField] private AttackPredictionFactory _attackPredictionFactory;
        [SerializeField] private Vector3 _predictionSize;
        [SerializeField] private float _predictionEndTime;

        [Header("攻撃目標表示設定")]
        [Tooltip("視線の先に攻撃目標地点を表示するマーカー (ローカル表示のみ)")]
        [SerializeField] private KrakenAimMarker _aimMarker;
        [Tooltip("視線の先に何も無かった場合に目標地点とする距離")]
        [SerializeField] private float _aimFallbackDistance = 20f;

        [Header("インタラクト設定")]
        [SerializeField] private InteractableBase _interactable;

        [Header("ダメージ設定")]
        [SerializeField] private int _dealScore = 10;

        [Header("出現時間設定")]
        [Tooltip("誰にもインタラクトされなかった場合に自動的に退場するまでの時間")]
        [SerializeField] private float _stayDuration = 20f;

        [Header("アニメーション設定")]
        [SerializeField] private PlayableDirector _playableDirector;
        [SerializeField] private TimelineAsset _inTimeline;
        [SerializeField] private TimelineAsset _outTimeline;

        [Header("触手設定")]
        [SerializeField] private KrakenTentacles _tentacles;

        [Header("攻撃設定")]
        [SerializeField] private KrakenAttackHandler _attackHandler;
        [SerializeField] private KrakenSettings _settings;

        private InputWrapper _attack;

        private KrakenAimPointResolver _aimPointResolver;

        private Vector3 _initialPosition;
        private Quaternion _initialRotation;

        private Vector3 _originalPlayerPosition;
        private Quaternion _originalPlayerRotation;

        private KrakenAppearanceState _appearanceState;

        [Networked] private TickTimer DisappearTimer { get; set; }

        public KrakenTentacles Tentacles => _tentacles;
        public ObjectPool<ParticleSystem> SlamParticlePool { get; private set; }

        // === IDamageable実装 ===
        public bool IsAlive => true; // ダメージを受けるだけで死亡しない
        public PlayerRef OwnerPlayerRef { get; private set; }

        private void Start()
        {
            _initialPosition = transform.position;
            _initialRotation = transform.rotation;

            _cameraController.Init(true);
            _attackHandler.Initialize(_tentacles.Arms, _settings, this);
            _aimPointResolver = new KrakenAimPointResolver(_settings.AttackPointRayHitLayer);

            SlamParticlePool = new ObjectPool<ParticleSystem>(
                () => Instantiate(_settings.SlamEffect),
                actionOnGet: particle => particle.gameObject.SetActive(true),
                actionOnRelease: particle => particle.gameObject.SetActive(false),
                defaultCapacity: _settings.DefaultParticlePoolCapacity
            );
        }

        /// <summary>
        /// ローカルでの入力処理
        /// </summary>
        private void LateUpdate()
        {
            if (!HasInputAuthority)
            {
                // 操作していないクライアントでは目標地点を表示しない
                if (_aimMarker != null) _aimMarker.Hide();
                return;
            }

            if (GameInput.I.Player.Aim.triggered)
            {
                _cameraController.CameraReset();
            }

            _cameraController.RotateCamera(GameInput.I.Player.Look.ReadValue<Vector2>(), Runner.DeltaTime);

            UpdateAimMarker();
        }

        /// <summary>
        /// 視線の先の攻撃目標地点にマーカーを表示する (ローカルのみ)
        /// </summary>
        private void UpdateAimMarker()
        {
            if (_aimMarker == null) return;

            if (_aimPointResolver.TryResolve(out KrakenAimPoint aimPoint))
            {
                _aimMarker.Show(aimPoint);
            }
            else
            {
                _aimMarker.Hide();
            }
        }

        public override void Spawned()
        {
            Appear().Forget();
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasInputAuthority) return;

            if (GetInput<PlayerInput>(out var input))
            {
                _attack.SetInput(input.Buttons.IsSet(PlayerButtons.Attack));

                if (_attack.IsJustPressed && _aimPointResolver.TryResolve(out KrakenAimPoint aimPoint))
                {
                    RPC_Attack(aimPoint.Position);
                }
            }
        }

        public override void Render()
        {
            // 誰かに操作されている最中であれば自動退場しない
            if (Object.InputAuthority != default) return;

            // 一定時間放置されたら自動的に退場する
            if (DisappearTimer.Expired(Runner) && _appearanceState == KrakenAppearanceState.Staying)
            {
                Disappear().Forget();
            }
        }

        /// <summary>
        /// 指定のプレイヤーをクラーケンにする
        /// </summary>
        /// <param name="owner"> </param>
        public void GetOn(PlayerRef owner)
        {
            // カメラを有効化する
            RPC_ResetCamera(owner);
            RPC_SetCameraPriority(owner, CameraPriority);

            // 元のプレイヤーオブジェクトを取得
            var playerObject = Runner.GetPlayerObject(owner);
            if (playerObject == null)
            {
                Debug.LogWarning($"{owner}のプレイヤーオブジェクトが見つかりませんでした");
                return;
            }

            _originalPlayerPosition = playerObject.transform.position;
            _originalPlayerRotation = playerObject.transform.rotation;

            playerObject.transform.position = transform.position;
            playerObject.transform.rotation = transform.rotation;

            // 元のプレイヤーオブジェクトを非表示にする
            if (playerObject.TryGetComponent<PlayerManager>(out var playerManager))
            {
                playerManager.RPC_SetInvisible(true);
            }

            // このプレイヤーから入力を受け取るように設定する
            Object.AssignInputAuthority(owner);

            _interactable.ForceSetInteractable = false;

            OwnerPlayerRef = owner;
            _settings.OwnerPlayerRef = owner;
        }

        /// <summary>
        /// 指定プレイヤーのクラーケン状態を解除する
        /// </summary>
        public void GetOff(PlayerRef owner)
        {
            // カメラを無効化する
            RPC_SetCameraPriority(owner, 0);

            // 元のプレイヤーオブジェクトを取得
            var playerObject = Runner.GetPlayerObject(owner);
            if (playerObject == null)
            {
                Debug.LogWarning($"{owner}のプレイヤーオブジェクトが見つかりませんでした");
                return;
            }

            playerObject.transform.position = _originalPlayerPosition;
            playerObject.transform.rotation = _originalPlayerRotation;

            // 元のプレイヤーオブジェクトを表示する
            var playerManager = playerObject.GetComponent<PlayerManager>();
            if (playerManager != null) playerManager.RPC_SetInvisible(false);

            // 初期トランスフォームに戻す
            transform.position = _initialPosition;
            transform.rotation = _initialRotation;

            // 入力を受け取らないようにする
            Object.RemoveInputAuthority();

            OwnerPlayerRef = default;
            _settings.OwnerPlayerRef = default;

            Disappear().Forget();
        }

        private async UniTaskVoid Appear()
        {
            _appearanceState = KrakenAppearanceState.Appear;
            await _playableDirector.PlayAsync(_inTimeline);
            DisappearTimer = TickTimer.CreateFromSeconds(Runner, _stayDuration);
            _appearanceState = KrakenAppearanceState.Staying;
        }

        private async UniTaskVoid Disappear()
        {
            _appearanceState = KrakenAppearanceState.Disappear;
            _interactable.ForceSetInteractable = false;
            await _playableDirector.PlayAsync(_outTimeline);
            if (HasStateAuthority) Runner.Despawn(Object);
        }

        [Rpc(RpcSources.All, RpcTargets.All)]
        private void RPC_Attack(Vector3 targetPosition)
        {
            Attack(targetPosition);
        }

        public void Attack(Vector3 targetPosition)
        {
            if (!_attackHandler.IsReady) return;

            if (!_attackHandler.TryGetTentacle(out var tentacle)) return;

            _attackHandler.Attack(tentacle, targetPosition).Forget();

            Vector3 dir = targetPosition - tentacle.ArmRoot.position;
            dir.y = 0;
            Quaternion lookRotation = Quaternion.LookRotation(dir);
            PredictionParticle particle = _attackPredictionFactory.Create(new AttackPredictionShape(targetPosition, _predictionSize, lookRotation));
            DestroyPredictionAreaAsync(tentacle, particle).Forget();
        }

        /// <summary>
        /// 腕を叩きつけた際に攻撃予測表示を非表示にし、攻撃予測範囲に攻撃判定を配置する
        /// </summary>
        private async UniTaskVoid DestroyPredictionAreaAsync(TentacleController tentacle, PredictionParticle particle)
        {
            await UniTask.WaitForSeconds(_predictionEndTime);
            _attackHandler.StartAreaAttack(tentacle, particle);
            particle.Destroy();
        }

        public void TakeHit(ref HitData hitData)
        {
            if (HasStateAuthority && OwnerPlayerRef.IsRealPlayer && hitData.HitActionType == HitActionType.Damage)
            {
                PlayerDatabase.Instance.Server_AddKrakenDamageScore(hitData.ExecutorRef, _dealScore);
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_ResetCamera(PlayerRef player)
        {
            if (player != Runner.LocalPlayer) return;

            _cameraController.CameraReset();
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_SetCameraPriority(PlayerRef player, int priority)
        {
            if (player != Runner.LocalPlayer) return;

            _cameraController.SetCameraPriority(priority);
        }

        private enum KrakenAppearanceState
        {
            Appear,
            Staying,
            Disappear,
        }
    }
}
