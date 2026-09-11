using System.Collections.Generic;
using System.Linq;
using CRISound;
using Cysharp.Threading.Tasks;
using Fusion;
using InGame.Player;
using Result;
using September.Common;
using September.InGame;
using September.InGame.Common;
using September.InGame.Effect;
using September.InGame.UI;
using UnityEngine;

namespace InGame.Interact
{
    /// <summary>
    /// プレイヤーがインタラクトできるオブジェクトの基底クラス
    /// 展示物などのインタラクト可能なオブジェクトに必要な共通機能を提供する
    /// - クールダウン管理
    /// - エフェクト再生（インタラクト完了、クールダウン）
    /// - 音声再生
    /// - バリデーション処理（クールダウン中、ゲーム終了、プレイヤースタン状態）
    /// - 展示物タイプの登録とログ表示
    /// </summary>
    [RequireComponent(typeof(AudioBroadcaster))] // サウンド再生用のコンポーネント
    [DisallowMultipleComponent]
    public class InteractableBase : NetworkBehaviour
    {
        /// <summary>キャラクタータイプごとのインタラクト所要時間（秒）</summary>
        [SerializeField] private SerializableDictionary<CharacterType, float> _requiredInteractTimeDictionary = new();

        /// <summary>キャラクタータイプごとのクールダウン時間（秒）</summary>
        [SerializeField] private SerializableDictionary<CharacterType, float> _cooldownTimeDictionary = new();

        /// <summary>キャラクタータイプごとのインタラクト効果リスト</summary>
        [SerializeReference, SubclassSelector] private List<CharacterInteractEffectBase> _characterEffects = new();

        /// <summary>この展示物のタイプ</summary>
        [SerializeField] private ExhibitType _type;
        /// <summary>インタラクトエフェクトの位置オフセット</summary>
        [SerializeField] private Vector3 _interactEffectOffset = Vector3.zero;
        /// <summary>クールダウンエフェクトを再生するTransform（未設定の場合は自身）</summary>
        [SerializeField] private Transform _cooldownEffectTransform;
        /// <summary>クールダウンエフェクトの位置オフセット</summary>
        [SerializeField] private Vector3 _cooldownEffectOffset = Vector3.zero;
        /// <summary>クールダウンエフェクトの回転（Euler角）</summary>
        [SerializeField] private Vector3 _cooldownEffectRotation = Vector3.zero;
        /// <summary>クールダウンエフェクトのスケール</summary>
        [SerializeField] private Vector3 _cooldownEffectScale = Vector3.one;
        /// <summary>インタラクト完了時のエフェクトタイプ</summary>
        [SerializeField] private EffectType _interactEffectType = EffectType.NormalInteractComplete;
        /// <summary>クールダウン中のエフェクトタイプ</summary>
        [SerializeField] private EffectType _cooldownEffectType = EffectType.CooldownSquare;
        /// <summary>クールダウンエフェクトをインタラクト直後に再生するか</summary>
        [SerializeField] private bool _spawnCooldownEffectOnStart = true;
        /// <summary>サウンド再生用コンポーネント</summary>
        [SerializeField] private AudioBroadcaster _audioBroadcaster;
        /// <summary>インタラクト時のサウンドCue名</summary>
        [SerializeField] private string _interactSoundCueName;
        /// <summary>サウンドの再生タイプ（2D/3D）</summary>
        [SerializeField] private SoundTrackingType _interactSoundTrackingType = SoundTrackingType.Spot;

        /// <summary>最後にインタラクトされた時刻（ネットワーク同期）</summary>
        [Networked] public float LastInteractTime { get; set; } = -9999f;

        /// <summary>最後に使用されたクールダウン時間（ネットワーク同期）</summary>
        [Networked] public float LastUsedCooldownTime { get; set; } = 0f;

        /// <summary>
        /// 外部から強制的にインタラクト可否を設定するフラグ（ネットワーク同期）
        /// falseに設定するとインタラクトを無効化できる
        /// </summary>
        [Networked]
        public bool ForceSetInteractable { get; set; } = true;

        /// <summary>インタラクト所要時間の読み取り専用プロパティ</summary>
        public SerializableDictionary<CharacterType, float> RequiredInteractTimeDictionary =>
            _requiredInteractTimeDictionary;

        /// <summary>クールダウン時間の読み取り専用プロパティ</summary>
        public SerializableDictionary<CharacterType, float> CooldownTimeDictionary => _cooldownTimeDictionary;

        /// <summary>展示物タイプの読み取り専用プロパティ</summary>
        public ExhibitType ExhibitType => _type;

        /// <summary>AudioBroadcasterの読み取り専用プロパティ</summary>
        public AudioBroadcaster AudioBroadcaster => _audioBroadcaster;

        /// <summary>現在アクティブなインタラクト効果</summary>
        private CharacterInteractEffectBase _activeEffectBase;

        private CharacterType _characterType;

        [SerializeField] private bool _debug;

        /// <summary>
        /// インタラクトのメイン処理（Host側でのみ実行）
        /// バリデーション → クールダウン設定 → エフェクト再生 → 効果発動 → 展示物登録・音声再生・ログ表示
        /// </summary>
        /// <param name="context">インタラクトのコンテキスト（実行者、キャラクタータイプ）</param>
        public void Interact(IInteractableContext context)
        {
            // Host側でのみ実行
            if (!HasStateAuthority) return;

            // バリデーションチェック（クールダウン、ゲーム状態など）
            if (!ValidateInteraction(context))
            {
                Debug.Log($"[InteractableBase] OnValidateInteraction により拒否: {context.Interactor}");
                return;
            }

            _characterType = context.CharacterType;

            // CharacterType.All を優先、なければキャラ固有のクールダウン時間を取得
            var cooldownTime = _cooldownTimeDictionary.Dictionary.TryGetValue(CharacterType.All, out var all)
                ? all
                : _cooldownTimeDictionary.Dictionary.GetValueOrDefault(_characterType, 0f);

            // クールダウンのループエフェクトを再生（設定がONで、クールダウン時間が0より大きい場合）
            if (_spawnCooldownEffectOnStart && cooldownTime > 0f)
            {
                SetCooldown(cooldownTime);
            }

            // インタラクト完了エフェクトを再生（ワンショット）
            var effectSpawner = StaticServiceLocator.Instance.Get<EffectSpawner>();
            effectSpawner.RequestPlayOneShotEffect(_interactEffectType, transform.position + _interactEffectOffset, transform.rotation);

            // 派生クラスの個別処理を実行
            OnInteract(context);

            // インタラクト実行者のPlayerRefを取得
            PlayerRef actor = PlayerRef.FromEncoded(context.Interactor);

            // 展示物タイプが設定されていればPlayerDatabaseに登録
            if (_type != ExhibitType.None)
            {
                PlayerDatabase.Instance.Server_AddExhibit(actor, _type);
            }

            // インタラクト音を再生
            if (_audioBroadcaster != null)
            {
                _audioBroadcaster.RPC_PlaySoundFromCode(_interactSoundCueName, _interactSoundTrackingType, Object, actor);
            }

            // 全クライアントにインタラクトログを表示
            Rpc_ShowInteractLog(actor, _type);
        }

        /// <summary>
        /// クールダウンを開始する。すでにクールダウン中であればクールダウン時間を上書きする
        /// </summary>
        public void SetCooldown(float seconds)
        {
            if (!HasStateAuthority || seconds <= 0f)
            {
                Debug.LogWarning($"[InteractableBase] クールダウンの開始に失敗しました\n詳細：{HasStateAuthority} {seconds}");
                return;
            }

            // すでにクールダウン中か評価する。（クールダウンの設定前に評価しないと常に真となるため事前に行う必要あり）
            var isInCooldown = IsInCooldown();

            // インタラクト時刻を記録（NetworkRunnerがあればシミュレーション時刻、なければローカル時刻）
            LastInteractTime = Runner ? Runner.SimulationTime : Time.time;
            LastUsedCooldownTime = seconds;

            // 二重にエフェクトが表示されないようクールダウン中なら処理しない
            if (!isInCooldown)
            {
                PlayCooldownEffect().Forget();
            }
        }
        
        /// <summary>
        /// クールダウンを開始する 
        /// </summary>
        private void StartCooldown()
        {
            var time = CooldownTimeDictionary.Dictionary.TryGetValue(CharacterType.All, out var all)
                ? all
                : CooldownTimeDictionary.Dictionary.GetValueOrDefault(_characterType, 0f);
            SetCooldown(time);
        }

        /// <summary>
        /// クールダウンエフェクトを再生する非同期処理
        /// クールダウン終了後にエフェクトを停止し、回復音を再生
        /// </summary>
        private async UniTask PlayCooldownEffect()
        {
            var effectSpawner = StaticServiceLocator.Instance.Get<EffectSpawner>();
            var effectTransform = _cooldownEffectTransform != null ? _cooldownEffectTransform : transform;

            // ループエフェクトを開始
            var effectId = effectSpawner.RequestPlayLoopEffect(_cooldownEffectType,
                effectTransform.position + _cooldownEffectOffset, Quaternion.Euler(_cooldownEffectRotation),
                _cooldownEffectScale);

            // クールダウン終了待機
            await UniTask.WaitUntil(this, s => !s.IsInCooldown(), cancellationToken: this.GetCancellationTokenOnDestroy());

            // エフェクトを停止
            effectSpawner.StopEffect(effectId);

            // クールダウン回復音を全クライアントで再生
            Rpc_PlaySE(SoundCues.SE.Exhibit_Revive.Sheet, SoundCues.SE.Exhibit_Revive.Name, effectTransform.position);
        }

        /// <summary>
        /// 全クライアントでSEを再生するRPC
        /// </summary>
        /// <param name="sheet">サウンドシート名</param>
        /// <param name="cueName">サウンドCue名</param>
        /// <param name="position">再生位置</param>
        [Rpc]
        private void Rpc_PlaySE(string sheet, string cueName, Vector3 position)
        {
            CRIAudio.PlaySE(position, sheet, cueName);
        }

        /// <summary>
        /// インタラクト可否の共通バリデーション
        /// クールダウン中、オブジェクト無効、強制無効化、派生クラスの条件をチェック
        /// </summary>
        /// <param name="context">インタラクトのコンテキスト</param>
        /// <returns>true: インタラクト可能、false: インタラクト不可</returns>
        public bool ValidateInteraction(IInteractableContext context)
        {
            var type = context.CharacterType;

            // クールダウン中はインタラクト不可
            if (IsInCooldown())
            {
                if (_debug) Debug.Log("[InteractableBase] クールダウン中のためインタラクトできません");
                return false;
            }

            // オブジェクトが無効な場合はインタラクト不可
            if (!Object.isActiveAndEnabled)
            {
                if (_debug) Debug.Log($"[{name}] インタラクト可能なオブジェクトが無効です");
                return false;
            }

            // 強制無効化フラグがfalseの場合はインタラクト不可
            if (!ForceSetInteractable)
            {
                if (_debug) Debug.Log($"[{name}] インタラクト可能なオブジェクトが強制的に無効化されています");
                return false;
            }

            // 派生クラスの個別条件をチェック
            if (!OnValidateInteraction(context, type))
            {
                if (_debug) Debug.Log($"[{name}] インタラクト可能なオブジェクトが OnValidateInteraction により拒否されました");
                return false;
            }

            return true;
        }

        /// <summary>
        /// インタラクト可能条件
        /// </summary>
        /// <param name="context">インタラクトのコンテキスト</param>
        /// <param name="charaType">キャラクタータイプ</param>
        /// <returns>true: インタラクト可能、false: インタラクト不可</returns>
        private bool OnValidateInteraction(IInteractableContext context, CharacterType charaType)
        {
            // ゲーム終了状態の時はインタラクトを無効化
            if (IsGameEnded())
            {
                return false;
            }

            // プレイヤーがスタン状態の時はインタラクトを無効化
            if (IsPlayerStunned(context))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// ゲームが終了状態（EndingState）かどうかを判定
        /// エラー時は安全側でインタラクトを許可（falseを返す）
        /// </summary>
        /// <returns>true: ゲーム終了状態、false: それ以外</returns>
        private bool IsGameEnded()
        {
            try
            {
                var inGameManager = StaticServiceLocator.Instance.Get<InGameManager>();
                if (inGameManager == null) return false;

                // EndingStateかどうかをチェック
                return inGameManager.CurrentStateName == "EndingState";
            }
            catch (System.Exception)
            {
                // エラーが発生した場合は安全側でインタラクトを許可
                return false;
            }
        }

        /// <summary>
        /// プレイヤーがスタン状態かどうかを判定
        /// エラー時は安全側でインタラクトを許可（falseを返す）
        /// </summary>
        /// <param name="context">インタラクトのコンテキスト</param>
        /// <returns>true: スタン状態、false: それ以外</returns>
        private bool IsPlayerStunned(IInteractableContext context)
        {
            try
            {
                // インタラクト実行者のPlayerRefを取得
                PlayerRef playerRef = PlayerRef.FromEncoded(context.Interactor);

                var inGameManager = StaticServiceLocator.Instance.Get<InGameManager>();
                if (inGameManager == null) return false;

                // プレイヤーのNetworkObjectを取得
                if (inGameManager.PlayerDataDic.TryGetValue(playerRef, out NetworkObject playerObject))
                {
                    // PlayerManagerを取得してスタン状態をチェック
                    var playerManager = playerObject.GetComponent<PlayerManager>();
                    if (playerManager != null)
                    {
                        return playerManager.IsStun;
                    }
                }
            }
            catch (System.Exception)
            {
                // エラーが発生した場合は安全側でインタラクトを許可
            }

            return false;
        }

        /// <summary>
        /// 派生クラスで個別のインタラクト処理を実装する仮想メソッド
        /// デフォルトではキャラクタータイプに応じた効果を選択して実行
        /// </summary>
        /// <param name="context">インタラクトのコンテキスト</param>
        protected virtual void OnInteract(IInteractableContext context)
        {
            var charaType = context.CharacterType;

            // CharacterType.All を優先、なければキャラ固有の効果を取得
            var effect = _characterEffects.FirstOrDefault(e => e != null && e.IsExecutable(charaType));

            if (effect != null)
            {
                // 効果をクローンして開始
                _activeEffectBase = effect.Clone();
                _activeEffectBase.OnInteractStart(context, this);
            }
            else
            {
                Debug.LogWarning($"[{name}] {charaType} のインタラクト効果が設定されていません");
            }
        }

        /// <summary>
        /// 現在クールダウン中かどうかを判定
        /// </summary>
        /// <returns>true: クールダウン中、false: クールダウン終了</returns>
        public bool IsInCooldown()
        {
            if (LastUsedCooldownTime <= 0f) return false;
            var currentTime = Runner ? Runner.SimulationTime : Time.time;
            float timeSinceLast = currentTime - LastInteractTime;
            return timeSinceLast < LastUsedCooldownTime;
        }

        /// <summary>
        /// 通常更新処理（Host側でのみ実行）
        /// アクティブな効果のOnInteractUpdate()を呼び出す
        /// </summary>
        private void Update()
        {
            if (!HasStateAuthority) return;
            _activeEffectBase?.OnInteractUpdate(Time.deltaTime);
        }

        /// <summary>
        /// 遅延更新処理（Host側でのみ実行）
        /// アクティブな効果のOnInteractLateUpdate()を呼び出す
        /// </summary>
        private void LateUpdate()
        {
            if (!HasStateAuthority) return;
            _activeEffectBase?.OnInteractLateUpdate(Time.deltaTime);
        }

        /// <summary>
        /// 物理更新処理（Host側でのみ実行）
        /// アクティブな効果のOnInteractFixedUpdate()を呼び出す
        /// </summary>
        private void FixedUpdate()
        {
            if (!HasStateAuthority) return;
            _activeEffectBase?.OnInteractFixedUpdate();
        }

        /// <summary>
        /// ネットワーク同期された固定更新処理
        /// アクティブな効果のOnInteractFixedNetworkUpdate()を呼び出す
        /// </summary>
        public override void FixedUpdateNetwork()
        {
            GetInput(out PlayerInput input);
            _activeEffectBase?.OnInteractFixedNetworkUpdate(input);
        }

        /// <summary>
        /// コリジョン接触中の処理（Host側でのみ実行）
        /// アクティブな効果のOnInteractCollisionStay()を呼び出す
        /// </summary>
        /// <param name="collision">コリジョン情報</param>
        private void OnCollisionStay(Collision collision)
        {
            if (!HasStateAuthority) return;
            _activeEffectBase?.OnInteractCollisionStay(collision);
        }

        /// <summary>
        /// インタラクト終了処理
        /// 外部またはクールダウンなどから呼び出される
        /// </summary>
        public void EndInteract()
        {
            StartCooldown();
            _activeEffectBase?.OnInteractEnd();
            ControlDescriptionType type = CharacterDataContainer.Instance.GetControlDescriptionType(_characterType);
            RPC_ChangeDescriptionUI(type);
            _activeEffectBase = null;
            _characterType = CharacterType.None;
        }

        /// <summary>
        /// InputAuthorityに説明UIの変更を指示するRPC
        /// </summary>
        /// <param name="mode">UIモード</param>
        [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
        private void RPC_ChangeDescriptionUI(ControlDescriptionType mode)
        {
            UIController.I.ChangeDescriptionUI(mode);
        }

        /// <summary>
        /// 全クライアントにインタラクトログを表示するRPC
        /// </summary>
        /// <param name="actor">インタラクト実行者のPlayerRef</param>
        /// <param name="exhibitType">展示物タイプ</param>
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void Rpc_ShowInteractLog(PlayerRef actor, ExhibitType exhibitType)
        {
            if (PlayerDatabase.Instance.PlayerDataDic.TryGet(actor, out var data))
            {
                string actorName = data.DisplayNickName;
                UIController.I.ShowLog($"{actorName} が {exhibitType.ToDisplayName()} にインタラクトしました");
            }
        }

        /// <summary>
        /// オフセットを考慮した位置を取得
        /// </summary>
        public Vector3 GetInteractPosition()
        {
            Vector3 result = this.transform.position;
            result.y += _cooldownEffectOffset.y;
            return result;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // クールダウンエフェクトの位置と向きを表示
            var effectTransform = _cooldownEffectTransform != null ? _cooldownEffectTransform : transform;
            var effectPos = effectTransform.position + _cooldownEffectOffset;
            var effectRot = Quaternion.Euler(_cooldownEffectRotation);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(effectPos, 0.3f);

            // 向きを矢印で表示（Forward=青, Up=緑, Right=赤）
            float arrowLength = 0.8f;
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(effectPos, effectRot * Vector3.forward * arrowLength);
            Gizmos.color = Color.green;
            Gizmos.DrawRay(effectPos, effectRot * Vector3.up * arrowLength);
            Gizmos.color = Color.red;
            Gizmos.DrawRay(effectPos, effectRot * Vector3.right * arrowLength);

            // インタラクトエフェクトの位置を表示
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position + _interactEffectOffset, 0.2f);
        }
#endif
    }
}

/// <summary>
/// インタラクト時のコンテキスト情報を保持するインターフェース
/// </summary>
public interface IInteractableContext : INetworkStruct
{
    /// <summary>インタラクト実行者のPlayerRef（エンコード済み）</summary>
    int Interactor { get; }
    /// <summary>インタラクト実行者のキャラクタータイプ</summary>
    CharacterType CharacterType { get; set; }
}

/// <summary>
/// IInteractableContextのシンプルな実装
/// 必要に応じて情報を追加可能
/// </summary>
public struct InteractableContext : IInteractableContext
{
    public int Interactor { get; set; }
    public CharacterType CharacterType { get; set; }
}
