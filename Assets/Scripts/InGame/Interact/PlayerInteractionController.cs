using System;
using System.Collections.Generic;
using Fusion;
using InGame.Bot;
using InGame.Player;
using InGame.Player.Ability;
using September.Common;
using September.InGame;
using September.InGame.Common.Stats;
using September.InGame.UI;
using UnityEngine;

namespace InGame.Interact
{
    [DisallowMultipleComponent]
    public class PlayerInteractionController : NetworkBehaviour
    {
        [SerializeField] private PlayerInputManager _inputManager;
        [SerializeField] private float _interactRadius = 2.5f;
        [SerializeField] private LayerMask _interactMask;
        [SerializeField, Range(0f, 180f)] private float _interactAngle = 90f; // 前方180度
        [SerializeField] private Transform _interactOrigin;
        [SerializeField] private float _baseInteractTime = 1.0f;
        [SerializeField] private float _ogreInteractMultiplier = 1.0f;
        [SerializeField] private CharacterType _characterType = CharacterType.OkabeWright;
        [SerializeField] private float _interactResponseTimeout = 3f;
        [SerializeField] private float _interactAngleBuffer = 10f; // 角度に+10°
        [SerializeField] private float _interactRadiusBuffer = 0.3f; // 距離に+0.3m
        [SerializeField] private PlayerAudioController _playerAudioController; // インタラクト時のボイス再生用
        [Header("ビルドシステム関連の参照")]
        [SerializeField] BuildGenerator _buildGenerator;
        [SerializeField] PlayerStatus _playerStatus;
        //許容できる高さの差
        private float _heightDifference = 0.1f;

        private bool _isWaitingForResponse = false;
        private float _interactWaitTimer = 0f;
        private readonly Collider[] _hitBuffer = new Collider[32];
        private InteractableBase _focusedObj;
        private bool _isExecutingInteraction = false;
        private float _currentInteractTime = 0f;
        private float _requiredInteractTime = 1.0f;
        [SerializeField] private bool _isHoldingInteract = false;
        private bool _hasCompletedInteraction = false;
        private PlayerManager _playerManager;
        private bool _isBot;

        [Networked] private bool IsRemoting { get; set; } //遠距離インタラクション中かの判定
        [Networked] private InteractableBase RemoteFocusedObject { get; set; } //遠インタラクションの対象オブジェクト
        [Networked] private float RemoteInteractTime { get; set; } //遠インタラクション時間
        [Networked] private float RemoteInteractTimer { get; set; } //遠インタラクションのタイマー

        private void Awake()
        {
            if (!_interactOrigin)
                _interactOrigin = transform;
            _playerManager = GetComponent<PlayerManager>();
            _playerAudioController = GetComponentInChildren<PlayerAudioController>();

#if UNITY_EDITOR
            if (_buildGenerator & _playerStatus)
                Debug.Log("ビルドシステムが正常に動きます");
            else
                Debug.LogWarning("ビルドに関する参照がないためビルドシステムが正常に動作しません\nPlayerInteractionController.csを確認してください", this);
#endif

            if (_inputManager == null)
                _inputManager = GetComponent<PlayerInputManager>();

            _isBot = _inputManager?.GetType() == typeof(BotInputManager);
        }

        public override void Spawned()
        {
            if (PlayerDatabase.Instance?.PlayerDataDic == null)
            {
                _characterType = CharacterType.OkabeWright;
            }
            else
            {
                _characterType = PlayerDatabase.Instance.PlayerDataDic[Object.InputAuthority].CharacterType;
            }
        }

        private void Update()
        {
            if (!HasInputAuthority && !_isBot) return;

            // ローカルでインタラクト対象を毎フレーム検出（カメラ向きで変化するため）
            UpdateFocusedInteractable();

            if (_isHoldingInteract)
            {
                if (!_isExecutingInteraction)
                    TryStartInteraction();

                if (_isExecutingInteraction)
                {
                    _currentInteractTime += Time.deltaTime;
                    if (_currentInteractTime >= _requiredInteractTime)
                    {
                        _hasCompletedInteraction = true;
                        CompleteInteraction();
                        _playerAudioController?.PlayInteractActionVoice();  // インタラクト時ボイスの再生依頼
                        if (!_isBot)
                        {
                            UIController.I.ShowInteractUI(false); // 終了時に消すだけならここでもOK
                        }
                    }
                    if (!_isBot)
                    {
                        UIController.I.SetInteractProgress(Mathf.Clamp01(_currentInteractTime / _requiredInteractTime));
                    }
                }
            }
            else
            {
                if (IsRemoting) //遠距離インタラクション中
                {
                    //インタラクションオブジェクトが存在する場合、インタラクションUIを表示する
                    if (RemoteFocusedObject)
                    {
                        var context = new InteractableContext
                        {
                            Interactor = Object.InputAuthority.RawEncoded,
                        };
                        if (UIController.I)
                        {
                            var isRiding = _playerManager && _playerManager.CurrentPlayerControlState ==
                                PlayerManager.PlayerControlState.ForcedControl;
                            UIController.I.ShowInteractUI(!isRiding && RemoteFocusedObject.ValidateInteraction(context),
                                RemoteFocusedObject?.gameObject);
                        }
                        UIController.I.SetInteractProgress(Mathf.Clamp01(RemoteInteractTimer / RemoteInteractTime));
                    }
                    return;
                }
                CancelInteraction();
            }
        }

        public override void FixedUpdateNetwork()
        {
            _isHoldingInteract = false; // 毎フレームリセット

            if (!HasInputAuthority && !_isBot) return;
            if (!_inputManager.GetPlayerInput(out PlayerInput input)) return;

            // Fusionのシミュレーション内でのみ行う処理
            if (_isWaitingForResponse)
            {
                _interactWaitTimer += Runner.DeltaTime;
                if (_interactWaitTimer >= _interactResponseTimeout)
                {
                    Debug.LogWarning("インタラクト応答タイムアウト: ロック解除");
                    _isWaitingForResponse = false;
                    _interactWaitTimer = 0f;
                }
                return;
            }

            _isHoldingInteract = input.Buttons.IsSet(PlayerButtons.Interact);
        }

        /// <summary>
        /// 遠距離からのインタラクション用
        /// </summary>
        /// <param name="timer">インタラクションタイマー</param>
        /// <param name="time">インタラクション必要時間</param>
        /// <param name="interactableBase">Rayで当たったインタラクション可能なオブジェクト</param>>
        /// <param name="abilityPhase">アビリティの状態</param>>
        /// <param name="aimCameraController">このAbilityを使っているキャラのカメラ</param>>
        public void RemoteInteraction(ref float timer, float time, InteractableBase interactableBase,
            ref AbilityBase.AbilityPhase abilityPhase, AimCameraController aimCameraController)
        {
            var context = new InteractableContext
            {
                Interactor = Object.InputAuthority.RawEncoded,
            };
            if (!interactableBase.ValidateInteraction(context)) return;

            var isRiding = _playerManager && _playerManager.CurrentPlayerControlState ==
                PlayerManager.PlayerControlState.ForcedControl;
            _focusedObj = interactableBase;
            UIController.I.ShowInteractUI(!isRiding && _focusedObj.ValidateInteraction(context), _focusedObj?.gameObject);

            IsRemoting = true;
            RemoteFocusedObject = interactableBase;

            timer += Runner.DeltaTime;
            RemoteInteractTime = time;
            RemoteInteractTimer = timer;
            if (timer >= time) //インタラクション成功時間を超えたらインタラクションを行う
            {
                IsRemoting = false;
                timer = 0f;
                RemoteInteractTimer = 0f;
                CompleteInteraction();
                UIController.I.ShowInteractUI(false);

                //インタラクションに成功したらアビリティを終了
                abilityPhase = AbilityBase.AbilityPhase.Ending;
                aimCameraController.RPC_NormalCamera();
                aimCameraController.RPC_CrosshairToggleChange(false);
            }
            UIController.I.SetInteractProgress(Mathf.Clamp01(timer / time));
        }

        /// <summary>
        ///　途中で遠距離インタラクションを中止したときに呼ぶ
        /// ・インタラクションの入力を辞めたとき
        /// ・エイムをインタラクションオブジェクトから外した時
        /// </summary>
        public void RemoteInteractionCancel(ref float timer)
        {
            IsRemoting = false;
            timer = 0;
            RemoteInteractTimer = 0f;
            CancelInteraction();
        }

        private void UpdateFocusedInteractable()
        {
            // 現在の focusedObj がまだ有効な範囲内かチェック
            if (_focusedObj && !IsRemoting)
            {
                if (!IsInInteractRange(_focusedObj.transform.position, InteractRangeCheckMode.Buffered))
                {
                    _focusedObj = null;
                    Debug.Log("Nullにする");
                }
            }

            //別のインタラクションオブジェクトに上書きされないようにする
            if (IsRemoting) return;

            // より近い候補があれば差し替え
            int count = Physics.OverlapSphereNonAlloc(_interactOrigin.position, _interactRadius, _hitBuffer,
                _interactMask);
            float closestDistanceSqr = _focusedObj ? (_focusedObj.transform.position - _interactOrigin.position).sqrMagnitude
                : float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                var go = _hitBuffer[i].gameObject;
                var interactable = go.GetComponentInParent<InteractableBase>()
                                   ?? go.GetComponent<InteractableBase>()
                                   ?? go.GetComponentInChildren<InteractableBase>();
                if (interactable == null) continue;

                Vector3 targetPos = interactable.transform.position;
                if (!IsInInteractRange(targetPos)) continue;

                float distanceSqr = (targetPos - _interactOrigin.position).sqrMagnitude;
                if (distanceSqr < closestDistanceSqr)
                {
                    closestDistanceSqr = distanceSqr;
                    _focusedObj = interactable;
                }
            }

            //Playerが処理する
            if (!_isBot)
            {
                UpdateInteractUI();
            }
        }

        private void UpdateInteractUI()
        {
            bool focusTargetIsValid = _focusedObj && _focusedObj.Id.IsValid;

            if (focusTargetIsValid)
            {
                var context = new InteractableContext
                {
                    Interactor = Object.InputAuthority.RawEncoded,
                };
                if (UIController.I)
                {
                    var isRiding = _playerManager && _playerManager.CurrentPlayerControlState ==
                        PlayerManager.PlayerControlState.ForcedControl;
                    UIController.I.ShowInteractUI(!isRiding && _focusedObj.ValidateInteraction(context), _focusedObj?.gameObject);
                }
            }
            else
            {
                if (UIController.I)
                    UIController.I.ShowInteractUI(false);
            }
        }

        /// <summary>
        /// 指定されたワールド座標が、インタラクトの有効範囲内（前方角度・距離）にあるかチェック
        /// </summary>
        private enum InteractRangeCheckMode
        {
            Strict, // 通常判定
            Buffered // バッファ許容
        }

        private bool IsInInteractRange(Vector3 targetPosition, InteractRangeCheckMode mode = InteractRangeCheckMode.Strict)
        {
            Vector3 toTarget = targetPosition - _interactOrigin.position;
            float radius = mode == InteractRangeCheckMode.Strict
                ? _interactRadius
                : _interactRadius + _interactRadiusBuffer;

            return toTarget.sqrMagnitude <= radius * radius &&
                   gameObject.transform.position.y - targetPosition.y <= _heightDifference;
        }

        private void TryStartInteraction()
        {
            if (!_focusedObj) return;

            _requiredInteractTime = GetRequireInteractTime() * (_playerStatus ? _playerStatus.InteractDurationMultiply : 1);

            var context = new InteractableContext
            {
                Interactor = Object.InputAuthority.RawEncoded,
            };
            if (!_focusedObj.ValidateInteraction(context))
            {
                return;
            }

            _currentInteractTime = 0f;
            _isExecutingInteraction = true;
            _hasCompletedInteraction = false;
        }

        private float GetRequireInteractTime()
        {
            if (!_focusedObj)
                return _baseInteractTime;
            var dict = _focusedObj.RequiredInteractTimeDictionary.Dictionary;
            float baseTime =
                dict.TryGetValue(CharacterType.All, out var allVal) ? allVal :
                dict.GetValueOrDefault(_characterType, _baseInteractTime);

            float multiplier = 1f;
            if (PlayerDatabase.Instance.PlayerDataDic.TryGet(Object.InputAuthority, out var playerData) &&
                playerData.IsOgre)
                multiplier = _ogreInteractMultiplier;

            return baseTime * multiplier;
        }

        private void CompleteInteraction()
        {
            _isExecutingInteraction = false;

            _buildGenerator?.UpdateBuild(BuildRouteType.FastInteract);

            if (GetSessionPlayerData(Object.InputAuthority.RawEncoded, out var data))
            {
                return;
            }
            var context = new InteractableContext
            {
                Interactor = Object.InputAuthority.RawEncoded,
                CharacterType = data.CharacterType,
            };

            if (HasStateAuthority)
            {
                _focusedObj?.Interact(context);
            }
            else
            {
                if (!_focusedObj)
                {
                    Debug.LogWarning("[Interact] _focusedObj is null");
                    return;
                }

                var netObj = _focusedObj.GetComponentInParent<NetworkObject>();
                if (!netObj)
                {
                    Debug.LogWarning($"[Interact] {_focusedObj.name} に NetworkObject が存在しません");
                    return;
                }

                // 応答待ちモードに入る
                _isWaitingForResponse = true;
                _interactWaitTimer = 0f;

                Debug.Log($"[Client] RPC_RequestInteract 送信: {context.Interactor} -> {_focusedObj.name} NetObj is null? {!netObj}");
                RPC_RequestInteract(context.Interactor, (int)context.CharacterType, netObj);
            }
        }

        private void CancelInteraction()
        {
            _isExecutingInteraction = false;
            _currentInteractTime = 0f;
            if (!_isBot)
            {
                UIController.I?.SetInteractProgress(0f);
            }
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        private void RPC_RequestInteract(int interactor, int characterType, NetworkObject target)
        {
            Debug.Log($"target.HasStateAuthority: {target.HasStateAuthority}, Runner.LocalPlayer: {Runner.LocalPlayer}");

            InteractableBase interactable;
            if (target && (interactable = target.GetComponentInChildren<InteractableBase>()) != null)
            {
                var context = new InteractableContext
                {
                    Interactor = interactor,
                    CharacterType = (CharacterType)characterType
                };

                interactable.Interact(context);
            }
        }

        private static bool GetSessionPlayerData(int interactor, out SessionPlayerData data)
        {
            if (!PlayerDatabase.Instance.PlayerDataDic.TryGet(PlayerRef.FromEncoded(interactor), out data))
            {
                Debug.LogWarning("[InteractableBase] インタラクト実行者のデータが見つかりません: " + interactor);
                return true;
            }

            return false;
        }
    }
}
