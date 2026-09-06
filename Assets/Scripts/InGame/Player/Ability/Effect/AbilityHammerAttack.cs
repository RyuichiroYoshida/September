using CRISound;
using Fusion;
using InGame.Exhibit.InteractEffect;
using InGame.Health;
using InGame.Interact;
using September.Common;
using September.InGame;
using UnityEngine;

namespace InGame.Player.Ability.Effect
{
    public class AbilityHammerAttack : AbilityNormalAttack
    {
        [Header("オーディオ")]
        [SerializeField] private AudioBroadcaster _audio;

        [Header("AbilityHammerAttack")]
        [SerializeField] private HammerAttackSettings _settings;

        private static readonly string CueName = "SE_Hulk_Break";

        protected override void OnHitEnemy(Collider hitInfo, Vector3 hitPosition)
        {
            if (hitInfo.GetComponentInParent<NetworkObject>() == Parameter.Owner) return;

            var damageable = hitInfo.GetComponentInParent<IDamageable>();
            var interactable = hitInfo.gameObject.GetComponentInHierarchy<InteractableBase>();

            if (damageable == null && !interactable) return;

            // 鬼状態かどうかでダメージを変更
            int damage = GetAttackDamage();

            if (damageable != null)
            {
                var hitData = new HitData(
                    HitActionType.Damage,
                    damage,
                    Parameter.Owner.InputAuthority,
                    damageable.OwnerPlayerRef);
                damageable.TakeHit(ref hitData);
                _buildGenerator?.UpdateBuild(BuildRouteType.AttackPower);
            }

            if (interactable
                && !interactable.IsInCooldown()
                && interactable.ForceSetInteractable
                && _settings.TryGetDisableDuration(interactable.ExhibitType, out float duration))
            {
                interactable.SetCooldown(duration);
                PlayerRef actor = Parameter.Owner.InputAuthority;
                _audio.RPC_PlaySoundFromCode(CueName, SoundTrackingType.Spot, default, actor);
                PlayerDatabase.Instance.Server_AddDestroyExhibit(actor, interactable.ExhibitType);
            }

            //エフェクトの再生
            _effectSpawner.RequestPlayOneShotEffect(_hitEffect, hitInfo.ClosestPoint(hitInfo.bounds.ClosestPoint(hitPosition)), Quaternion.identity);
        }
    }
}
