using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Fusion;
using InGame.Common;
using InGame.Interact;
using InGame.Player;
using InGame.Player.Ability;
using September.Common;
using UnityEngine;

namespace InGame.Exhibit
{
    public class ArmoryInteractEffect : CharacterInteractEffectBase
    {
        [SerializeReference, SubclassSelector] private List<AbilityBase> _addAbilities;
        [SerializeReference, SubclassSelector] private List<IAbilityExecuteCondition> _addAbilityConditions;
        [SerializeField] private string[] _overrideDisabledAbilities;
        [SerializeField] private float _duration;

        public override CharacterInteractEffectBase Clone()
        {
            return new ArmoryInteractEffect
            {
                _addAbilities = _addAbilities.Select(CloneUtility.CloneObject).ToList(),
                _addAbilityConditions = _addAbilityConditions.Select(CloneUtility.CloneObject).ToList(),
                _overrideDisabledAbilities = _overrideDisabledAbilities,
                _duration = _duration
            };
        }

        public override void OnInteractStart(IInteractableContext context, InteractableBase target)
        {
            //コンポーネント取得
            var player = PlayerRef.FromEncoded(context.Interactor);
            if (!PlayerDatabase.Instance.PlayerObjectDic.TryGet(player, out var playerObject))
            {
                Debug.LogError($"インタラクトしたプレイヤーが見つかりません。{player}");
                return;
            }

            if (!playerObject.TryGetComponent(out PlayerAbilityManager playerAbility)
                || !playerObject.TryGetComponent(out PlayerEquipmentManager equipmentManager))
            {
                return;
            }

            //上書きするAbilityを無効化
            playerAbility.SetAbilityEnabled(false, _overrideDisabledAbilities);

            //Abilityを追加する
            for (int i = 0; i < Mathf.Min(_addAbilities.Count, _addAbilityConditions.Count); i++)
            {
                _addAbilities[i].SetPlayerComponent(playerObject.gameObject);
                playerAbility.AddAbility(_addAbilities[i], _addAbilityConditions[i]);
            }

            equipmentManager.RPC_ChangeEquipment(EquipmentType.Armory);

            ReleaseWeaponAsync(playerAbility, equipmentManager, playerObject.GetCancellationTokenOnDestroy()).Forget();
        }

        private async UniTask ReleaseWeaponAsync(PlayerAbilityManager abilityManager, PlayerEquipmentManager equipmentManager, CancellationToken token)
        {
            await UniTask.WaitForSeconds(_duration, cancellationToken: token);

            //無効化していたAbilityを有効化
            abilityManager.SetAbilityEnabled(true, _overrideDisabledAbilities);

            //アビリティ実行途中なら待機
            await UniTask.WaitUntil(_addAbilities, abilities => abilities.All(x => x.Phase != AbilityBase.AbilityPhase.Active), cancellationToken: token);

            //追加したAbilityを消す
            foreach (var ability in _addAbilities)
            {
                abilityManager.RemoveAbility(ability.GetType().Name);
            }

            //武器を元に戻す
            equipmentManager.RPC_ChangeEquipment(EquipmentType.NormalAttack);
        }
    }
}
