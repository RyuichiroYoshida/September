using Fusion;
using InGame.Common;
using InGame.Interact;
using September.Common;
using System;
using Ingame.Tanihira;
using InGame.Player;
using UnityEngine;
using UnityEngine.Splines;

namespace September
{
    [Serializable]
    public class ZipLineInteractEffect : CharacterInteractEffectBase
    {
        public AnimationClip Anim;
        public SplineContainer Spline;
        public GameObject Trolley;
        public float Duration = 5f;
        public float ReturnDuration = 5f;
        [Header("横軸:経過時間の割合(0〜1) 縦軸:スプライン上の位置の割合(0〜1)" +
            "\n始点(t=0)は必ず0、終点(t=1)は必ず1に設定してください")]

        public AnimationCurve SpeedCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [Tooltip("Trolleyからプレイヤーへの相対オフセット(ぶら下がる位置調整用)")]
        public Vector3 PlayerOffset = new Vector3(0f, -1.5f, 0f);
        private InteractableBase _activeEffect;
        private Animator _animator;
        private enum State
        {
            Idle,
            Moving,
            Returning
        }

        private State _currentState = State.Idle;

        private float _timer;
        private NetworkObject _targetPlayerObject;

        public override void OnInteractStart(IInteractableContext context, InteractableBase target)
        {
            PlayerRef playerRef = PlayerRef.FromEncoded(context.Interactor);
            if (PlayerDatabase.Instance.PlayerObjectDic.TryGet(playerRef, out var playerNetworkObject))
            {
                _targetPlayerObject = playerNetworkObject;

                // プレイヤーの落下アニメーションを停止する
                if (_targetPlayerObject.TryGetComponent(out AnimationClipPlayerManager animManager))
                {
                    animManager.EnableFallMotion = false;
                }

                // プレイヤーのジップラインに掴まるアニメーションを再生する
                if (_targetPlayerObject.TryGetComponent(out AnimationClipPlayer animationClipPlayer))
                {
                    animationClipPlayer.PlayClipLoop(Anim);
                }
            }
            else
            {
                Debug.LogError("[ZipLineInteractEffect] Player not found");
            }
            _activeEffect = target;
            _activeEffect.ForceSetInteractable = false;

            _targetPlayerObject.GetComponent<FormationManager>()?.WarpFriendOutField();

            _animator = _targetPlayerObject.GetComponentInChildren<Animator>();
            Trolley.transform.position = Spline.EvaluatePosition(0f);
            // 乗車開始時はスプライン始点の接線方向から台車の向きを設定する。
            FaceTravelDirection((Vector3)Spline.EvaluateTangent(0f));
            _timer = 0f;
            _currentState = State.Moving;
            MovePlayerToTrolley();

            var playerManager = _targetPlayerObject.GetComponent<PlayerManager>();
            // 見た目とカメラの台車への追従を開始する。
            playerManager.BeginRideView(Trolley.transform, PlayerOffset);
            playerManager.RPC_SetUseGrav(false);
            playerManager.SetControlState(PlayerManager.PlayerControlState.ForcedControl);
        }

        public override void OnInteractUpdate(float deltaTime)
        {
            switch (_currentState)
            {
                case State.Moving:
                    UpdateMoving(deltaTime);
                    break;
                case State.Returning:
                    UpdateReturning(deltaTime);
                    break;
            }
        }

        private void UpdateMoving(float deltaTime)
        {
            if (_targetPlayerObject == null) return;

            _timer += deltaTime;
            float t = Mathf.Clamp01(_timer / Duration);
            float evaluatedT = Mathf.Clamp01(SpeedCurve.Evaluate(t));
            // 速度カーブで求めたスプライン上の位置へ台車を進める。
            MoveTrolley(evaluatedT);
            MovePlayerToTrolley();

            if (t >= 1f)
            {
                var completedPlayer = _targetPlayerObject;

                // プレイヤーをここで降ろす
                var playerManager = completedPlayer.GetComponent<PlayerManager>();
                // 台車が始点へ戻り始める前に、見た目・カメラを通常の追従へ戻す。
                playerManager.EndRideView();
                playerManager.RPC_SetUseGrav(true);
                playerManager.SetControlState(PlayerManager.PlayerControlState.Normal);

                // プレイヤーのアニメーションを停止する
                if (completedPlayer.TryGetComponent(out AnimationClipPlayer animationClipPlayer))
                {
                    animationClipPlayer.StopClip(Anim);
                }

                // プレイヤーの落下アニメーションを再度有効にする
                if (completedPlayer.TryGetComponent(out AnimationClipPlayerManager animManager))
                {
                    animManager.EnableFallMotion = true;
                }

                if (completedPlayer.TryGetComponent(out FormationManager formationManager) &&
                    completedPlayer.TryGetComponent(out PlayerMovement playerMovement))
                {
                    formationManager.WarpFriendNearPlayerWhenGrounded(playerMovement);
                }

                _targetPlayerObject = null;

                // Trolleyだけ始点へ戻すフェーズへ
                _timer = 0f;
                _currentState = State.Returning;
            }
        }

        private void UpdateReturning(float deltaTime)
        {
            _timer += deltaTime;
            float t = Mathf.Clamp01(_timer / ReturnDuration);

            float evaluatedT = SpeedCurve.Evaluate(t);
            // スプラインの終点から始点へ戻り、戻る方向に台車を向ける。
            MoveTrolley(Mathf.Clamp01(1f - evaluatedT));

            if (t >= 1f)
            {
                Trolley.transform.position = Spline.EvaluatePosition(0f);
                _activeEffect.EndInteract(); // ここで初めてInteractableBase側の終了処理を呼ぶ
            }
        }

        private void MovePlayerToTrolley()
        {
            // 状態権限を持つ側で、プレイヤー本体を台車位置＋乗車オフセットへ移動する。
            if (_targetPlayerObject == null) return;
            if (!_targetPlayerObject.HasStateAuthority) return;
            if (_targetPlayerObject.TryGetComponent(out PlayerMovement movement))
                // プレイヤー本体の位置と回転を台車に合わせて更新する。
                movement.TeleportImmediate(Trolley.transform.position + PlayerOffset, Trolley.transform.rotation);
        }

        private void MoveTrolley(float splinePosition)
        {
            // スプライン上の座標を取得し、移動前の座標との差から進行方向を求める。
            Vector3 position = Spline.EvaluatePosition(splinePosition);
            Vector3 direction = position - Trolley.transform.position;
            Trolley.transform.position = position;
            // 座標の更新後、移動方向の水平成分へ向きを合わせる。
            FaceTravelDirection(direction);
        }

        private void FaceTravelDirection(Vector3 direction)
        {
            // 高さの変化を除いた進行方向へ向ける。停止中・垂直移動中は直前の向きを保つ。
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.000001f)
                Trolley.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }

        public override void OnInteractEnd()
        {
            // 乗車中のプレイヤーの追従を終了し、重力・通常操作・落下モーションを戻す。
            if (_targetPlayerObject != null)
            {
                var playerManager = _targetPlayerObject.GetComponent<PlayerManager>();
                playerManager.EndRideView();
                playerManager.RPC_SetUseGrav(true);
                playerManager.SetControlState(PlayerManager.PlayerControlState.Normal);
                if (_targetPlayerObject.TryGetComponent(out AnimationClipPlayer clip)) clip.StopClip(Anim);
                if (_targetPlayerObject.TryGetComponent(out AnimationClipPlayerManager animation)) animation.EnableFallMotion = true;
                _targetPlayerObject = null;
            }
            // Returning完了後にInteractableBase.EndInteract()経由で呼ばれる
            _currentState = State.Idle;

            if (_activeEffect != null)
            {
                _activeEffect.ForceSetInteractable = true; // ここで初めて使用可能に戻す
                _activeEffect = null;
            }
        }

        public override CharacterInteractEffectBase Clone()
        {
            return new ZipLineInteractEffect
            {
                Anim = Anim,
                Spline = Spline,
                Trolley = Trolley,
                Duration = Duration,
                ReturnDuration = ReturnDuration,
                SpeedCurve = SpeedCurve,
                PlayerOffset = PlayerOffset
            };
        }
    }
}
