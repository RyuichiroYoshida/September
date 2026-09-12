using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;

namespace InGame.Player.Ability
{
    /// <summary>
    /// プレイヤーのアビリティシステムを管理するクラス
    /// 入力条件とアビリティの実行を結びつけ、条件に応じてアビリティを開始・更新する
    /// </summary>
    public class PlayerAbilityManager : NetworkBehaviour
    {
        [SerializeField] private PlayerInputManager _playerInputManager;
        /// <summary>管理するアビリティのリスト（InspectorでSubclassSelectorで選択）</summary>
        [SerializeReference, SubclassSelector] private List<AbilityBase> _abilities = new();
        /// <summary>アビリティ実行条件のリスト（InspectorでSubclassSelectorで選択）</summary>
        [SerializeReference, SubclassSelector] private List<IAbilityExecuteCondition> _conditions = new();
        /// <summary>前フレームのボタン入力</summary>
        private NetworkButtons _previousButtons;
        /// <summary>現在のボタン入力</summary>
        private NetworkButtons _currentButtons;
        /// <summary>このプレイヤーのNetworkObject</summary>
        private NetworkObject _networkObject;
        /// <summary>アビリティ名（クラス名）でアビリティを検索するための辞書</summary>
        private readonly Dictionary<string, AbilityBase> _abilityByName = new();
        /// <summary>
        /// チュートリアルでアビリティ判定を行うため
        /// </summary>
        public AbilityBase CurrentAbility = null;

        /// <summary>
        /// ネットワーク生成時の初期化処理
        /// アビリティを辞書に登録する
        /// </summary>
        public override void Spawned()
        {
            _networkObject = GetComponent<NetworkObject>();
            // 全アビリティをクラス名をキーとして辞書に登録
            foreach (var ability in _abilities)
            {
                ability.IsEnabled = true;
                _abilityByName[ability.GetType().Name] = ability;
            }
        }

        /// <summary>
        /// 通常の更新処理（全クライアントで実行）
        /// ローカルプレイヤー専用の処理を実行
        /// </summary>
        private void Update()
        {
            // 全アビリティのローカル更新処理を実行（エフェクト、カメラ演出など）
            foreach (var ability in _abilities)
            {
                if (!ability.IsEnabled) continue;
                ability.OnUpdateLocal(Time.deltaTime, gameObject);
            }
        }

        /// <summary>
        /// ネットワーク同期された固定更新処理
        /// 入力を取得し、条件に応じてアビリティを開始・更新する
        /// </summary>
        public override void FixedUpdateNetwork()
        {
            // プレイヤー入力を取得（取得できない場合は処理をスキップ）
            if (_playerInputManager == null) return;
            if (!_playerInputManager.GetPlayerInput(out var input)) return;

            // 前フレームと現在のボタン入力を更新
            _previousButtons = _currentButtons;
            _currentButtons = input.Buttons;

            // Host側でのみアビリティの開始・更新を実行
            if (!HasStateAuthority) return;

            // 全実行条件をチェックし、条件を満たしたアビリティを開始
            foreach (var condition in _conditions)
            {
                // 条件に対応するアビリティを辞書から取得
                if (!_abilityByName.TryGetValue(condition.TargetAbilityName, out var targetAbility))
                {
                    Debug.LogError($"[PlayerAbilityManager] '{condition.TargetAbilityName}' が見つかりません。");
                    continue;
                }

                if (!targetAbility.IsEnabled) continue;

                // 条件判定用のコンテキストを作成
                var ctx = new TriggerEventContext(gameObject, targetAbility, _currentButtons, _previousButtons);
                // 条件を満たしていればアビリティを開始
                if (condition.IsConditionMatch(in ctx))
                {
                    targetAbility.Start(new AbilityParameter { Owner = _networkObject });
                    CurrentAbility = targetAbility;
                }
            }

            // 全アビリティを更新
            foreach (var ability in _abilities)
            {
                if (!ability.IsEnabled) continue;

                //入力を保持
                ability.SetPlayerInput(input);

                ability.Tick(Time.deltaTime);
            }
        }

        public void SetAbilityEnabled(bool enabled, params string[] types)
        {
            foreach (var type in types)
            {
                if (!_abilityByName.TryGetValue(type, out var targetAbility))
                {
                    continue;
                }

                targetAbility.IsEnabled = enabled;
            }
        }

        public void AddAbility(AbilityBase abilityBase, IAbilityExecuteCondition condition)
        {
            string abilityName = abilityBase.GetType().Name;

            if (_abilityByName.ContainsKey(abilityName))
            {
                Debug.LogError($"[PlayerAbilityManager] '{abilityName}' は既に追加されています。");
                return;
            }

            if (abilityName != condition.TargetAbilityName)
            {
                Debug.LogError($"[PlayerAbilityManager] {condition} では {abilityName} を実行できません。");
                return;
            }

            _abilities.Add(abilityBase);
            _conditions.Add(condition);
            _abilityByName[abilityName] = abilityBase;
        }

        public void RemoveAbility(string type)
        {
            if (!_abilityByName.TryGetValue(type, out var targetAbility))
                return;

            _abilities.Remove(targetAbility);

            var condition = _conditions.FirstOrDefault(x => x.TargetAbilityName == type);

            if (condition != null)
            {
                _conditions.Remove(condition);
            }

            _abilityByName.Remove(type);
        }
    }
}