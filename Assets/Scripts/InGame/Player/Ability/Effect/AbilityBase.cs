using Fusion;
using InGame.Health;
using September.Common;
using System;
using System.Linq;
using UnityEngine;

namespace InGame.Player.Ability
{
    /// <summary>
    /// アビリティに渡されるパラメータ
    /// </summary>
    public class AbilityParameter
    {
        /// <summary>
        /// アビリティを実行するプレイヤーのNetworkObject
        /// </summary>
        public NetworkObject Owner { get; set; }
    }

    /// <summary>
    /// アビリティの基底クラス
    /// フェーズ管理（Available → Started → Active → Ending → Cooldown → Available）を行う
    /// 派生クラスで個別のアビリティ動作を実装する
    /// </summary>
    [Serializable]
    public class AbilityBase : IHitExecutor
    {
        /// <summary>
        /// アビリティの実行フェーズ
        /// </summary>
        public enum AbilityPhase
        {
            /// <summary>使用可能な待機状態</summary>
            Available,
            /// <summary>開始された直後の状態（1フレームのみ）</summary>
            Started,
            /// <summary>実行中の状態。OnUpdate()が毎フレーム呼ばれる</summary>
            Active,
            /// <summary>終了処理中の状態（1フレームのみ）</summary>
            Ending,
            /// <summary>クールダウン中の状態</summary>
            Cooldown,
        }

        /// <summary>クールダウン時間（秒）</summary>
        [SerializeField] protected float _cooldown;
        /// <summary>現在のアビリティフェーズ</summary>
        [SerializeField] protected AbilityPhase _phase = AbilityPhase.Available;
        /// <summary> アビリティの有効、無効 </summary>
        [HideInInspector] public bool IsEnabled = true;
        /// <summary>NetworkRunnerのインスタンス（シミュレーション時刻取得用）</summary>
        protected NetworkRunner Runner => NetworkRunner.Instances.FirstOrDefault();

        /// <summary>
        /// アビリティのパラメータ。Start()で設定されるため、それより前にアクセスしないこと
        /// </summary>
        public AbilityParameter Parameter { get; set; }
        /// <summary>クールダウン終了時刻</summary>
        protected float CooldownEndTime { get; private set; }
        /// <summary>現在のアビリティフェーズ（読み取り専用）</summary>
        public AbilityPhase Phase => _phase;

        //inputを保持するために追加
        protected PlayerInput _playerInput;

        /// <summary>アビリティ開始時のティック</summary>
        public int StartTick { get; private set; }
        /// <summary>アビリティ開始時の時間</summary>
        public float StartTime { get; private set; }
        /// <summary>アビリティ開始からの経過ティック</summary>
        public int ElapsedTick => Runner ? Runner.Tick - StartTick : -1;
        /// <summary>アビリティ開始からの経過時間</summary>
        public float ElapsedTime => (Runner ? Runner.SimulationTime : Time.time) - StartTime;

        /// <summary>
        /// アビリティを開始する（PlayerAbilityManagerから呼び出される）
        /// </summary>
        /// <param name="parameter">アビリティのパラメータ（オーナー情報など）</param>
        public void Start(AbilityParameter parameter)
        {
            Parameter = parameter;
            _phase = AbilityPhase.Started;
            StartTick = Runner ? Runner.Tick : -1;
            StartTime = Runner ? Runner.SimulationTime : Time.time;
        }

        /// <summary>
        /// アビリティを更新する（PlayerAbilityManagerから毎フレーム呼び出される）
        /// </summary>
        /// <param name="deltaTime">フレーム間の経過時間</param>
        public void Tick(float deltaTime)
        {
            ProcessPhase(deltaTime);
        }

        /// <summary>
        /// アビリティを終了する
        /// </summary>
        public void RequestEndAbility()
        {
            _phase = AbilityPhase.Ending;
        }

        /// <summary>
        /// プレイヤーの入力を設定する
        /// </summary>
        /// <param name="playerInput">入力</param>
        public void SetPlayerInput(PlayerInput playerInput)
        {
            _playerInput = playerInput;
        }

        /// <summary>
        /// 現在のフェーズに応じて処理を振り分ける
        /// </summary>
        /// <param name="deltaTime">フレーム間の経過時間</param>
        private void ProcessPhase(float deltaTime)
        {
            switch (_phase)
            {
                case AbilityPhase.Started:
                    // 開始処理を実行してActiveに遷移
                    OnStart();
                    _phase = AbilityPhase.Active;
                    break;
                case AbilityPhase.Active:
                    // 実行中の更新処理
                    OnUpdate(deltaTime);
                    break;
                case AbilityPhase.Ending:
                    // 終了処理を実行してCooldownに遷移
                    OnEndAbility();
                    ResetCooldown();
                    _phase = AbilityPhase.Cooldown;
                    break;
                case AbilityPhase.Cooldown:
                    // クールダウン判定
                    OnCooldown();
                    break;
            }
        }

        /// <summary>
        /// クールダウン時刻を計算してセットする
        /// </summary>
        private void ResetCooldown()
        {
            // NetworkRunnerがあればシミュレーション時刻、なければローカル時刻を使用
            CooldownEndTime = Runner ? Runner.SimulationTime + _cooldown : Time.time + _cooldown;
        }

        /// <summary>
        /// クールダウン中の処理
        /// クールダウン終了時刻を超えたらAvailableフェーズに遷移
        /// </summary>
        private void OnCooldown()
        {
            // 現在時刻がクールダウン終了時刻を超えたら使用可能に戻す
            if (Runner ? Runner.SimulationTime >= CooldownEndTime : Time.time >= CooldownEndTime)
            {
                _phase = AbilityPhase.Available;
            }
        }

        /// <summary>
        /// アビリティが使用可能かどうかを判定（派生クラスで独自条件を追加可能）
        /// </summary>
        /// <returns>true: 使用可能、false: 使用不可</returns>
        public virtual bool CanStartAbilityOverride() => true;

        /// <summary>
        /// アビリティ開始時の処理（派生クラスでオーバーライド）
        /// </summary>
        protected virtual void OnStart() { }

        /// <summary>
        /// アビリティ実行中の更新処理（派生クラスでオーバーライド）
        /// </summary>
        /// <param name="deltaTime">フレーム間の経過時間</param>
        protected virtual void OnUpdate(float deltaTime) { }

        /// <summary>
        /// ローカルプレイヤー専用の更新処理（派生クラスでオーバーライド）
        /// カメラ演出やエフェクトなど、ローカルのみの処理が必要な場合に使用
        /// </summary>
        /// <param name="deltaTime">フレーム間の経過時間</param>
        /// <param name="owner">プレイヤーのGameObject</param>
        public virtual void OnUpdateLocal(float deltaTime, GameObject owner) { }

        /// <summary>
        /// AbilityにあるPlayerがもつComponentを取得する
        /// </summary>
        public virtual void SetPlayerComponent(GameObject player) { }

        /// <summary>
        /// アビリティ終了時の処理（派生クラスでオーバーライド可能）
        /// </summary>
        protected virtual void OnEndAbility() { }

        public void HitExecution(HitData hitData)
        {
            throw new NotImplementedException();
        }
    }
}
