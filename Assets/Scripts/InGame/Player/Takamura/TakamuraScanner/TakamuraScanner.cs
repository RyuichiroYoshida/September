using System;
using System.Collections.Generic;
using Fusion;
using InGame.Interact;
using September.Common;
using UnityEngine;

namespace InGame.Player
{
    /// <summary>スキャンに関する処理を持つクラス</summary>
    public class TakamuraScanner : NetworkBehaviour, IAfterTick
    {
        PlayerManager _playerManager;
        TakamuraMovement _movement;
        CameraController _cameraController;
        Camera _camera;
        NetworkButtons _preInput;

        [Networked, OnChangedRender(nameof(OnMimicTargetChanged))]
        NetworkId MimicTargetId { get; set; }

        [Header("カメラ制御")]
        [SerializeField, Tooltip("フォーカス時のカメラの位置")]
        Vector3 _focusPosition = new(0.5f, 1f, -2f);

        [SerializeField, Tooltip("カメラの移動時間")]
        float _cameraMoveDuration = 0.2f;

        [Header("カメラスキャンの有効領域")]
        [SerializeField, Tooltip("擬態対象の候補にできる最大距離")]
        float _scannableMaxDistance = 10f;

        [SerializeField, Tooltip("コライダーの対象レイヤー")]
        LayerMask _targetLayer;

        [SerializeField, Tooltip("演出用キャンバス")]
        ScannerCanvas _scannerCanvas;

        [Header("ガワ")]
        [SerializeField]
        TakamuraVisual _visual;

        TakamuraScanTarget[] _scanTargets = Array.Empty<TakamuraScanTarget>();
        readonly Dictionary<NetworkId, TakamuraScanTarget> _targetByNetworkId = new();
        int _focusIndex = -1;

        StateChangeType _pendingStateChange = StateChangeType.None;
        int _stateChangeTick = -1;

        enum StateChangeType
        {
            None,
            Mimic,
            Reveal
        }

        public TakamuraVisual Visual => _visual;

        public override void Spawned()
        {
            _playerManager = GetComponent<PlayerManager>();
            _movement = GetComponent<TakamuraMovement>();
            _scanTargets = FindObjectsByType<TakamuraScanTarget>(FindObjectsSortMode.None);
            CreateTargetDictionary();

            if (HasInputAuthority)
            {
                _cameraController = GetComponent<CameraController>();
                _camera = Camera.main;
            }

            _scannerCanvas.gameObject.SetActive(false);
            ChangeVisual();
        }

        public override void FixedUpdateNetwork()
        {
            if (HasStateAuthority) ApplyPendingStateChange();

            // inputにはこのオブジェクトに対する入力権限があるプレイヤーからの入力が入る
            if (!GetInput<PlayerInput>(out var input)) return;
            Ability2Flow(input);
        }

        public void AfterTick()
        {
            // Tickの終わりに前の入力を保存する
            if (GetInput<PlayerInput>(out var input))
            {
                _preInput = input.Buttons;
            }
        }

        /// <summary>
        /// Ability2の処理の流れを持つメソッド
        /// </summary>
        /// <param name="input">このオブジェクトに対する入力権限を持つプレイヤーからの入力</param>
        void Ability2Flow(PlayerInput input)
        {
            if (_movement.CurrentMimicryState == MimicryState.Default)
            {
                // フォーカスをあてる（地上にいる時のみ開始できる）
                if (_movement.IsGround && input.Buttons.WasPressed(_preInput, PlayerButtons.Ability2))
                {
                    if (HasInputAuthority) FocusStartEffective();
                    if (HasStateAuthority) FocusStartStateChange();
                }

                // フォーカス中
                if (input.Buttons.IsSet(PlayerButtons.Ability2))
                {
                    if (HasInputAuthority && _scannerCanvas.gameObject.activeSelf) FocusEffective(input);

                    // 擬態する（地上にいる時のみ実行できる）
                    if (_movement.IsGround
                        && input.Buttons.WasPressed(_preInput, PlayerButtons.Attack)
                        && HasInputAuthority)
                    {
                        if (_focusIndex == -1) return;

                        var target = _scanTargets[_focusIndex];
                        if (target == null) return;
                        var networkObject = target.GetComponentInParent<NetworkObject>();
                        if (networkObject == null) return;

                        RPC_Mimic(networkObject.Id);
                        FocusEndEffective();
                    }
                }

                // フォーカス解除（着地するまで入力の押下/離した判定が持ち越されてしまうため、
                // 空中にいる間も判定できるようにする）
                if (input.Buttons.WasReleased(_preInput, PlayerButtons.Ability2))
                {
                    if (HasInputAuthority)
                    {
                        FocusEndEffective();
                    }
                    if (HasStateAuthority) FocusEndStateChange();
                }
            }
            else if (_movement.CurrentMimicryState == MimicryState.MimicExhibit)
            {
                // 擬態解除する（地上にいる時のみ実行できる）
                if (_movement.IsGround
                    && input.Buttons.WasPressed(_preInput, PlayerButtons.Attack)
                    && HasInputAuthority)
                {
                    RPC_Reveal();
                }
            }
        }

        /// <summary>
        /// 全端末で擬態するためのメソッド
        /// </summary>
        /// <param name="targetId">擬態対象のNetworkId</param>
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
        void RPC_Mimic(NetworkId targetId)
        {
            if (!_targetByNetworkId.TryGetValue(targetId, out var target)) return;
            var interactable = target.GetComponentInParent<InteractableBase>();
            if (!interactable || _movement.CurrentMimicryState != MimicryState.Default) return;

            transform.position += Vector3.up;
            MimicTargetId = targetId;
            _movement.CurrentExhibitType = interactable.ExhibitType;
            ReserveStateChange(StateChangeType.Mimic);
        }

        /// <summary>
        /// 全端末で擬態解除するためのメソッド
        /// </summary>
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority, Channel = RpcChannel.Reliable)]
        void RPC_Reveal()
        {
            ReserveReveal();
        }

        /// <summary>
        /// 擬態解除メソッド
        /// </summary>
        void ReserveReveal()
        {
            if (_movement.CurrentMimicryState != MimicryState.MimicExhibit) return;
            transform.position += Vector3.up;
            ReserveStateChange(StateChangeType.Reveal);
        }

        /// <summary>
        /// 状態変更の予約メソッド
        /// </summary>
        /// <param name="stateChange"></param>
        void ReserveStateChange(StateChangeType stateChange)
        {
            _pendingStateChange = stateChange;
            _stateChangeTick = Runner.Tick + 1;
        }

        /// <summary>
        /// 状態変更を適用するメソッド
        /// </summary>
        void ApplyPendingStateChange()
        {
            if (_pendingStateChange == StateChangeType.None || Runner.Tick < _stateChangeTick) return;

            switch (_pendingStateChange)
            {
                case StateChangeType.Mimic:
                    _movement.CurrentMimicryState = MimicryState.MimicExhibit;
                    FocusEndStateChange();
                    break;
                case StateChangeType.Reveal:
                    MimicTargetId = default;
                    _movement.CurrentMimicryState = MimicryState.Default;
                    break;
            }

            _pendingStateChange = StateChangeType.None;
            _stateChangeTick = -1;
        }

        /// <summary>
        /// フォーカスを開始した時の演出メソッド
        /// </summary>
        void FocusStartEffective()
        {
            _scannerCanvas.gameObject.SetActive(true);
            _scannerCanvas.ChangeImageVisibility(false);
            _cameraController.ChangeOffset(_focusPosition, _cameraMoveDuration);
        }

        /// <summary>
        /// フォーカス中の演出メソッド
        /// </summary>
        /// <param name="input">プレイヤーが向く方向</param>
        void FocusEffective(PlayerInput input)
        {
            _movement.SetRotationDirection(input.DesiredLookDirection);
            UpdateNearestExhibit();
            FocusExhibit();
        }

        /// <summary>
        /// フォーカス終了時の演出メソッド
        /// </summary>
        void FocusEndEffective()
        {
            _cameraController.ResetOffset(_cameraMoveDuration);
            _scannerCanvas.ChangeImageVisibility(false);
            _scannerCanvas.gameObject.SetActive(false);
            _focusIndex = -1;
        }

        /// <summary>
        /// フォーカス開始時の状態変更メソッド
        /// </summary>
        void FocusStartStateChange()
        {
            _playerManager.SetControlState(PlayerManager.PlayerControlState.InputLocked);
            _movement.CurrentAbilityPhase = ScanAbilityPhase.Scanning;
        }

        /// <summary>
        /// フォーカス終了時の状態変更メソッド
        /// </summary>
        void FocusEndStateChange()
        {
            _playerManager.SetControlState(PlayerManager.PlayerControlState.Normal);
            _movement.CurrentAbilityPhase = ScanAbilityPhase.Default;
        }

        /// <summary>
        /// 条件に合った擬態対象を計算して取得するメソッド
        /// </summary>
        void UpdateNearestExhibit()
        {
            var moreCenter = float.MaxValue;
            _focusIndex = -1;

            foreach (var interactable in _scanTargets)
            {
                if (!interactable || !interactable.gameObject.activeSelf) continue;

                // 画面上の位置
                var scanPos = interactable.ScanPos;
                var position = scanPos ? scanPos.position : interactable.transform.position;
                var viewportPoint = _camera.WorldToViewportPoint(position);

                // 画面内にいなければスキップ
                if (viewportPoint.x < 0f || viewportPoint.x > 1f
                    || viewportPoint.y < 0f || viewportPoint.y > 1f
                    || viewportPoint.z < 0f)
                    continue;

                // 判定距離内かどうか
                var distance = Vector3.SqrMagnitude(position - transform.position);
                if (distance > _scannableMaxDistance * _scannableMaxDistance) continue;

                // より画面の中心にいるかどうか
                var center = (0.5f - viewportPoint.x) * (0.5f - viewportPoint.x)
                             + (0.5f - viewportPoint.y) * (0.5f - viewportPoint.y);
                if (center >= moreCenter) continue;

                // 壁越しかどうか判定
                var rayOrigin = _camera.transform.position;
                var rayDirection = position - rayOrigin;
                var rayDistance = rayDirection.magnitude;
                var hasHit = Physics.Raycast(
                    rayOrigin,
                    rayDirection.normalized,
                    out var hit,
                    rayDistance,
                    _targetLayer,
                    QueryTriggerInteraction.Collide);

                if (!hasHit)
                {
                    Debug.DrawLine(rayOrigin, position, Color.yellow);
                }
                else if (!IsHitScanTarget(hit.collider, interactable))
                {
                    Debug.DrawLine(rayOrigin, hit.point, Color.red);
                }
                else
                {
                    // 条件に合致した
                    Debug.DrawLine(rayOrigin, hit.point, Color.green);
                    moreCenter = center;
                    _focusIndex = Array.IndexOf(_scanTargets, interactable);
                }
            }
        }

        /// <summary>
        /// 壁越し判定メソッド
        /// </summary>
        /// <param name="hitCollider">Rayが当たったオブジェクト</param>
        /// <param name="target">想定している擬態対象</param>
        /// <returns>壁が間にないか</returns>
        bool IsHitScanTarget(Collider hitCollider, TakamuraScanTarget target)
        {
            if (!hitCollider || !target) return false;

            var hitNetworkObject = hitCollider.GetComponent<InteractableBase>();
            if (!hitNetworkObject)
                hitNetworkObject = hitCollider.GetComponentInParent<InteractableBase>();

            var targetNetworkObject = target.GetComponent<InteractableBase>();
            if (!targetNetworkObject)
                targetNetworkObject = target.GetComponentInParent<InteractableBase>();

            return hitNetworkObject && targetNetworkObject && hitNetworkObject == targetNetworkObject;
        }

        /// <summary>
        /// 擬態対象にフォーカスを合わせる演出メソッド
        /// </summary>
        void FocusExhibit()
        {
            var scanned = _focusIndex != -1;
            _scannerCanvas.ChangeImageVisibility(scanned);
            if (!scanned) return;

            var target = _scanTargets[_focusIndex];
            if (!target) return;

            var pivot = target.ScanPos;
            var position = _camera.WorldToScreenPoint(pivot ? pivot.position : target.transform.position);
            _scannerCanvas.SetImageOverExhibit(position);
        }

        /// <summary>
        /// 擬態対象のNetworkIdをキャッシュするメソッド
        /// </summary>
        void CreateTargetDictionary()
        {
            _targetByNetworkId.Clear();

            foreach (var target in _scanTargets)
            {
                if (!target) continue;

                var networkObject = target.GetComponentInParent<NetworkObject>();
                if (!networkObject || _targetByNetworkId.ContainsKey(networkObject.Id)) continue;
                _targetByNetworkId.Add(networkObject.Id, target);
            }
        }

        /// <summary>
        /// 擬態対象のIdが変わった時に呼ばれるメソッド
        /// </summary>
        void OnMimicTargetChanged()
        {
            ChangeVisual();
        }

        /// <summary>
        /// ガワを変更するメソッド
        /// </summary>
        void ChangeVisual()
        {
            if (!_visual) return;

            if (MimicTargetId == default)
            {
                _visual.Reveal();
                return;
            }

            if (_targetByNetworkId.TryGetValue(MimicTargetId, out var target) && target)
            {
                _visual.Mimic(target);
            }
        }
    }
}
