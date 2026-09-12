using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Fusion;
using InGame.Common;
using InGame.Health;
using InGame.Interact;
using InGame.Player;
using September.Common;
using Unity.Cinemachine;
using UnityEngine;

namespace September.InGame.Exhibit
{
	public class ProjectileInteractableBase : NetworkBehaviour
	{
		[SerializeField] protected CinemachineVirtualCamera _cameraController;
		[SerializeField] protected InteractableBase _interactable;
		[SerializeField] protected Animator _animator;
		[SerializeField] protected NetworkMecanimAnimator _networkAnimator;
		[SerializeField] private AnimationClip _playerUseAnimationClip;

		[Header("reload設定")] [SerializeReference] [SubclassSelector]
		public IFireController FireBulletController;

		[Header("レティクル設定")] [SerializeReference] [SubclassSelector]
		private IReticleEffect _reticleEffect;

		protected ProjectileLauncher _launcher;
		protected IProjectileMovement _move;
		protected PlayerManager _usingPlayer;
		private AnimationClipPlayer _animationClipPlayer;
		public event Action<int> OnAmmoChanged;

		[Networked] private NetworkButtons _attackButton { get; set; }
		[Networked] protected PlayerRef CurrentUsePlayerRef { get; set; }
		[Networked] protected TickTimer LastFireTimer { get; set; }
		[Networked] protected TickTimer WaitExitTimer { get; set; }
		[Networked] private TickTimer InteractEndLockTimer { get; set; }

		[Networked]
		[OnChangedRender(nameof(AmmoChanged))]
		private int CurrentAmmo { get; set; }

		public override void Spawned()
		{
			base.Spawned();
			_launcher = GetComponent<ProjectileLauncher>();
			_move = GetComponent<IProjectileMovement>();
			_reticleEffect?.Init();
		}

		public override void Render()
		{
			base.Render();
			_move?.Render();
			_reticleEffect?.Render();

			if (_animationClipPlayer && !_animationClipPlayer.IsPlayingTargetClip(_playerUseAnimationClip))
			{
				_animationClipPlayer.PlayClip(_playerUseAnimationClip);
			}
		}

		public override void FixedUpdateNetwork()
		{
			base.FixedUpdateNetwork();
			if (CurrentUsePlayerRef.IsNone) return;

			if (!GetInput(out PlayerInput input))
				return;
			_move.Update(input);

			if (LastFireTimer.Expired(Runner))
				FireBulletController.OnFireTimerExpired();

			// 射撃処理
			if (input.Buttons.WasPressed(_attackButton, PlayerButtons.Attack) &&
			    LastFireTimer.ExpiredOrNotRunning(Runner))
			{
				Fire();
				if (Runner.IsForward) PlayFireAnimation();
			}

			if (HasStateAuthority)
				CurrentAmmo = FireBulletController.CurrentAmmo;

			_attackButton = input.Buttons;

			CheckInteractEnd(input);
		}

		/// <summary>
		///     Hostのみで実行されるインタラクト開始時の初期化処理
		/// </summary>
		public virtual void InteractStart(PlayerRef playerRef)
		{
			// プレイヤーの取得
			CurrentUsePlayerRef = playerRef;
			_usingPlayer = Runner.GetPlayerObject(CurrentUsePlayerRef).GetComponent<PlayerManager>();

			// インタラクトの機能を一時的に無効化する
			_interactable.ForceSetInteractable = false;

			RPC_SetCameraPriority(CurrentUsePlayerRef, 15);
			RPC_StartAnimation(true);

			Object.AssignInputAuthority(CurrentUsePlayerRef);

			// 使用中のプレイヤーに対する処理
			if (!_usingPlayer) return;
			GetPlayerAnimatorClipPlayer(_usingPlayer);
			PlayerActive(false);
			_move.InitializeStateAuthority(_usingPlayer.Object, playerRef);
			FireBulletController.Init();
			CurrentAmmo = FireBulletController.CurrentAmmo;

			// Playerがダメージを受けた際にInteractを終了する
			_usingPlayer.GetComponent<PlayerHealth>().OnHitTaken += PlayerHitTaken;

			// インタラクトして1秒後からインタラクト解除可能にする
			InteractEndLockTimer = TickTimer.CreateFromSeconds(Runner, 1f);

			// 全てのクライアントで必要な初期化処理を行う
			RPC_AllClientInit(CurrentUsePlayerRef, true);
		}

		protected virtual void Fire()
		{
			if (!HasStateAuthority) return;
			// 発射処理はHostが扱う
			_launcher.Fire(CurrentUsePlayerRef);
			FireBulletController.Fire();

			if (!FireBulletController.IsUsable())
			{
				// IsUsableがfalseだった場合は終了処理に飛ばす
				var timer = TickTimer.CreateFromSeconds(Runner, 1f);
				WaitExitTimer = timer;
				LastFireTimer = timer;
			}
			else
			{
				LastFireTimer = FireBulletController.GetNextFireTimer(Runner);
			}
		}

		protected virtual void CheckInteractEnd(PlayerInput input)
		{
			if (!HasStateAuthority) return;

			if (input.Buttons.IsSet(PlayerButtons.Interact) && InteractEndLockTimer.ExpiredOrNotRunning(Runner))
				InteractEnd();

			if (WaitExitTimer.Expired(Runner))
			{
				WaitExitTimer = TickTimer.None;
				InteractEnd();
			}
		}

		/// <summary>
		///     インタラクト終了時の処理(Hostのみ)
		/// </summary>
		public void InteractEnd()
		{
			SetCooldown();
			_move.Reset();
			RPC_StartAnimation(false);
			Object.RemoveInputAuthority();
			RPC_SetCameraPriority(CurrentUsePlayerRef, 5);

			if (!_usingPlayer) return;
			PlayerActive(true);
			RPC_AllClientInit(CurrentUsePlayerRef, false);
			_usingPlayer.GetComponent<PlayerHealth>().OnHitTaken -= PlayerHitTaken;			
			AnimationEnd();


			_usingPlayer = null;
			CurrentUsePlayerRef = default;
			_interactable.EndInteract();
		}

		private void PlayerActive(bool isActive)
		{
			if (Runner.IsServer)
			{
				RPC_SetCameraPriority(CurrentUsePlayerRef, isActive ? 0 : 15);
				_usingPlayer.SetControlState(isActive
					? PlayerManager.PlayerControlState.Normal
					: PlayerManager.PlayerControlState.ForcedControl);
				_usingPlayer.RPC_SetUseGrav(isActive);

				if (_usingPlayer.TryGetComponent(out AnimationClipPlayerManager animationClipPlayerManager))
				{
					animationClipPlayerManager.EnableFallMotion = isActive;
				}
			}
		}

		private void SetCooldown()
		{
			// クールダウン処理
			var chara = PlayerDatabase.Instance.PlayerDataDic[CurrentUsePlayerRef].CharacterType;
			var time = _interactable.CooldownTimeDictionary.Dictionary.TryGetValue(CharacterType.All, out var all)
				? all
				: _interactable.CooldownTimeDictionary.Dictionary.GetValueOrDefault(chara, 0f);
			_interactable.SetCooldown(time);
			_interactable.ForceSetInteractable = true;
		}

		[Rpc]
		private void RPC_AllClientInit(PlayerRef currentPlayer, bool isActive)
		{
			EffectActive(currentPlayer, isActive);
			_move.Initialize();
		}

		private void GetPlayerAnimatorClipPlayer(PlayerManager playerManager)
		{
			// Playerのアニメーション適応
			if (_playerUseAnimationClip == null) return;
			if (playerManager.TryGetComponent(out AnimationClipPlayer playerManagerAnimationClipPlayer))
			{
				_animationClipPlayer = playerManagerAnimationClipPlayer;
			}
		}

		private void AnimationEnd()
		{
			_animationClipPlayer.StopClip(_playerUseAnimationClip);
			_animationClipPlayer = null;
		}

		protected virtual void EffectActive(PlayerRef currentPlayer, bool isActive)
		{
			_reticleEffect.AllClientEffectActive(isActive);
			if (Runner.LocalPlayer == currentPlayer) _reticleEffect?.SetActive(isActive);
		}

		private void PlayerHitTaken(HitData hitData)
		{
			InteractEnd();
		}

		[Rpc]
		private void RPC_StartAnimation(bool isActive)
		{
			if (!_animator) return;
			_animator.SetBool("IsStart", isActive);
		}

		private void PlayFireAnimation()
		{
			if (!_networkAnimator) return;
			_networkAnimator?.SetTrigger("Fire", true);
		}

		private void AmmoChanged()
		{
			OnAmmoChanged?.Invoke(CurrentAmmo);
		}

		#region Helper

		[Rpc(RpcSources.All, RpcTargets.All)]
		private void RPC_SetCameraPriority(PlayerRef playerRef, int priority)
		{
			if (Runner.LocalPlayer != playerRef) return;
			_cameraController.Priority = priority;
			_cameraController.MoveToTopOfPrioritySubqueue();
		}

		#endregion
	}

	public interface IProjectileMovement
	{
		public void InitializeStateAuthority(NetworkObject playerObject, PlayerRef playerRef);
		public void Initialize();
		public void Render();
		public void Update(PlayerInput input);
		public void Reset();
	}

	public interface IFireController
	{
		int CurrentAmmo { get; }
		void Init();
		void Fire();
		bool IsUsable();
		TickTimer GetNextFireTimer(NetworkRunner runner);
		void OnFireTimerExpired();
	}

	public interface IReticleEffect
	{
		void Init();
		void Render();
		void SetActive(bool active);
		void AllClientEffectActive(bool active);
	}

	[Serializable]
	public class UseReload : IFireController
	{
		[SerializeField] private int _maxAmmo;
		[SerializeField] private float _fireRate;
		[SerializeField] private float _reloadTime;
		private bool _isReloading;

		public int CurrentAmmo { get; private set; }

		public void Init()
		{
			CurrentAmmo = _maxAmmo;
			_isReloading = false;
		}

		public void Fire()
		{
			CurrentAmmo--;
			if (CurrentAmmo == 0) _isReloading = true;
		}

		public bool IsUsable()
		{
			return true;
		}

		public TickTimer GetNextFireTimer(NetworkRunner runner)
		{
			if (_isReloading) return TickTimer.CreateFromSeconds(runner, _reloadTime);

			return TickTimer.CreateFromSeconds(runner, _fireRate);
		}

		public void OnFireTimerExpired()
		{
			if (_isReloading)
				Init();
		}
	}

	[Serializable]
	public class NoReload : IFireController
	{
		[SerializeField] private int _maxAmmo;
		[SerializeField] private float _fireRate;

		public int CurrentAmmo { get; private set; }

		public void Init()
		{
			CurrentAmmo = _maxAmmo;
		}

		public void Fire()
		{
			CurrentAmmo--;
		}

		public bool IsUsable()
		{
			return 0 < CurrentAmmo;
		}

		public TickTimer GetNextFireTimer(NetworkRunner runner)
		{
			return TickTimer.CreateFromSeconds(runner, _fireRate);
		}

		public void OnFireTimerExpired()
		{
		}
	}
}