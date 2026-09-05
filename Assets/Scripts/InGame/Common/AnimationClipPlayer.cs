using System;
using System.Collections.Generic;
using System.Threading;
using Common.Extensions;
using Cysharp.Threading.Tasks;
using Fusion;
using September.InGame.Common;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace InGame.Common
{
    /// <summary>
    /// アニメーションをPlayableGraphで再生するコンポーネント。
    /// 永続するモーション（移動など）はBaseレイヤーに設定し、
    /// 一時的なモーションはFullBodyやUpperBodyレイヤーに設定して使う。
    /// RootMotion非対応。
    /// </summary>
    public class AnimationClipPlayer : NetworkBehaviour
    {
        [SerializeField] private List<LayerInfo> _layerInfo;
        [SerializeField, Range(0f, 10f)] private float _graphSpeed = 1f;
        [Header("移動アニメーション")]
        [SerializeField] private AnimationClip _wait;
        [SerializeField] private AnimationClip _walk;
        [SerializeField] private AnimationClip _run;
        [SerializeField, Range(0f, 2f)] private float _locoWeight = 0f;
        [SerializeField] protected Animator _animator;

        private PlayableGraph _graph;
        private AnimationPlayableOutput _output;
        private AnimationMixerPlayable _baseMixer;
        private AnimationLayerMixerPlayable _layerMixer;

        /// <summary>グラフ評価 (LateUpdate) の直前に呼ばれる。足 IK など出力後処理のパラメータ更新用。</summary>
        public event Action BeforeEvaluate;

        public PlayableGraph Graph => _graph;
        public Animator Animator => _animator;
        public AnimationClip WalkClip => _walk;
        public AnimationClip RunClip => _run;

        /// <summary>
        /// LayerMixerに登録しているInputSlotのindex
        /// </summary>
        private readonly Dictionary<LayerInfo.LayerType, int> _slotOf = new();
        private readonly Dictionary<LayerInfo.LayerType, AnimationClipPlayable> _runtimeClips = new();
        private readonly Dictionary<LayerInfo.LayerType, CancellationTokenSource> _layerCts = new();
        private readonly Dictionary<LayerInfo.LayerType, CancellationTokenSource> _weightBlendCts = new();

        /// <summary>
        /// 現在再生中のクリップ情報
        /// </summary>
        private readonly Dictionary<LayerInfo.LayerType, AnimationClip> _clipOf = new();

        public AnimationMixerPlayable BaseMixer => _baseMixer;

        public bool IsValid => _graph.IsValid();

        #region Initialize
        public void Start()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (!_animator && !TryGetComponent(out _animator))
                Debug.LogError("[AnimationClipPlayer] Animator がありません。");

            if (_layerInfo == null || _layerInfo.Count == 0)
            {
                Debug.LogError("LayerInfo を設定してください。（Base 含む）");
                enabled = false;
                return;
            }

            for (int i = 0; i < _layerInfo.Count; i++)
            {
                var t = _layerInfo[i].Type;
                if (!_slotOf.TryAdd(t, i))
                    Debug.LogWarning($"LayerType {t} が重複しています。最初の定義を採用します。");
            }

            if (!_slotOf.ContainsKey(LayerInfo.LayerType.Base))
            {
                Debug.LogError("Base レイヤーがありません。");
                enabled = false;
                return;
            }

            _graph = PlayableGraph.Create("AnimationClipPlayerGraph");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);

            _output = AnimationPlayableOutput.Create(_graph, "AnimationOutput", _animator);
            _output.SetWeight(1f);

            _layerMixer = AnimationLayerMixerPlayable.Create(_graph, _layerInfo.Count);
            _output.SetSourcePlayable(_layerMixer);

            _baseMixer = AnimationMixerPlayable.Create(_graph, 3);
            var baseSlot = _slotOf[LayerInfo.LayerType.Base];
            _graph.Connect(_baseMixer, 0, _layerMixer, baseSlot);
            _layerMixer.SetInputWeight(baseSlot, 1f);
            _layerMixer.SetLayerAdditive((uint)baseSlot, false);

            //各レイヤーの初期設定
            for (int i = 0; i < _layerInfo.Count; i++)
            {
                var li = _layerInfo[i];
                if (li.LayerMask) _layerMixer.SetLayerMaskFromAvatarMask((uint)i, li.LayerMask);
                _layerMixer.SetLayerAdditive((uint)i, li.Additive);
                if (i != baseSlot) _layerMixer.SetInputWeight(i, Mathf.Clamp01(li.Weight));
            }

            var port = 0;
            if (_wait)
            {
                var p = AnimationClipPlayable.Create(_graph, _wait);
                _baseMixer.ConnectInput(port++, p, 0);
            }
            else _baseMixer.SetInputWeight(port++, 0f);

            if (_walk)
            {
                var p = AnimationClipPlayable.Create(_graph, _walk);
                _baseMixer.ConnectInput(port++, p, 0);
            }
            else _baseMixer.SetInputWeight(port++, 0f);

            if (_run)
            {
                var p = AnimationClipPlayable.Create(_graph, _run);
                _baseMixer.ConnectInput(port, p, 0);
            }
            else _baseMixer.SetInputWeight(port, 0f);

            _graph.Play();
        }
        #endregion

        #region Update
        public void Update()
        {
            if (!_graph.IsValid())
            {
                Initialize();
            }

            UpdateLocoBlend(_locoWeight);
            // レイヤー重み（Base以外）
            foreach (var kv in _slotOf)
            {
                if (kv.Key == LayerInfo.LayerType.Base) continue;
                int i = kv.Value;
                _layerMixer.SetInputWeight(i, Mathf.Clamp01(_layerInfo[i].Weight));
            }
        }

        public void LateUpdate()
        {
            BeforeEvaluate?.Invoke();
            _graph.Evaluate(Time.deltaTime * _graphSpeed);
        }

        /// <summary>
        /// レイヤーミキサーと Animator 出力の間に後処理 Playable (足 IK ジョブ等) を差し込む。
        /// processor の入力 0 にレイヤーミキサーを接続する。グラフが無効なら false。
        /// グラフは再初期化されることがあるため、呼び出し側は Playable の IsValid で再差し込みを判断すること。
        /// </summary>
        public bool TryInstallOutputProcessor(Playable processor)
        {
            if (!_graph.IsValid() || !processor.IsValid() || !_layerMixer.IsValid()) return false;
            if (processor.GetInputCount() < 1)
            {
                Debug.LogError("[AnimationClipPlayer] 出力後処理 Playable は入力を 1 つ以上持つ必要があります。");
                return false;
            }

            processor.ConnectInput(0, _layerMixer, 0);
            processor.SetInputWeight(0, 1f);
            _output.SetSourcePlayable(processor);
            return true;
        }
        #endregion

        #region Play
        /// <summary>
        /// AnimationClipsContainerに登録されているMontageを再生します。
        /// Montageで設定されたレイヤーやブレンドに応じてアニメーションを制御します。
        /// </summary>
        /// <param name="clip">再生するアニメーション</param>
        public void PlayClip(AnimationClip clip)
        {
            // AnimationClipsContainer から探して再生
            if (!TryGetMontageIndex(clip, out int index))
            {
                Debug.LogWarning($"AnimationClip {clip.name} is not found in AnimationClipsContainer");
                return;
            }

            RPC_PlayAsync(index);
            PlayAsync(index);
        }

        /// <summary> 再生したClipが終了または中断されるまで待機 </summary>
        public async UniTask<EndClipType> PlayClipAndWait(AnimationClip clip)
        {
            // AnimationClipsContainer から探して再生
            if (!TryGetMontageIndex(clip, out int index))
            {
                Debug.LogWarning($"AnimationClip {clip.name} is not found in AnimationClipsContainer");
                return EndClipType.Failed;
            }

            RPC_PlayAsync(index);
            return await PlayAsync(index);
        }

        /// <summary> TopLayerでアニメーションを再生 </summary>
        public void PlayOnLayer(AnimationClip clip, LayerInfo.LayerType layerType = LayerInfo.LayerType.TopLayer, float speed = 1f, bool loop = false)
        {
            if (!_slotOf.TryGetValue(layerType, out var slot))
            {
                Debug.LogWarning($"[AnimationClipPlayer] {layerType} が設定されていません。_layerInfo の最後に追加してください。");
                return;
            }

            // 解除要求
            if (clip == null)
            {
                _layerMixer.SetInputWeight(slot, 0f);

                if (_runtimeClips.TryGetValue(layerType, out var current) && current.IsValid())
                {
                    DisconnectAndDestroy(layerType, current, slot);
                }

                var li0 = _layerInfo[slot];
                li0.Weight = 0f;
                _layerInfo[slot] = li0;
                return;
            }

            if (_runtimeClips.TryGetValue(layerType, out var prev) && prev.IsValid())
            {
                DisconnectAndDestroy(layerType, prev, slot);
            }

            Play(clip, layerType, 1f, playSpeed: speed, additive: false, loop: loop);

            var li = _layerInfo[slot];
            li.Weight = 1f; // Update() で毎フレーム反映されるので内部Weightも更新
            _layerInfo[slot] = li;
        }

        /// <summary>
        /// TopLayerで現在のアニメーションから指定したアニメーションへクロスフェードします。
        /// </summary>
        /// <param name="clip">遷移先のアニメーション</param>
        /// <param name="speed">遷移先の再生速度</param>
        /// <param name="blendTime">クロスフェード時間</param>
        /// <param name="blendCurve">クロスフェードの補間曲線</param>
        /// <param name="token">外部から遷移を中断するトークン</param>
        public async UniTask CrossFadeOnTopLayerAsync(
            AnimationClip clip,
            float speed,
            float blendTime,
            AnimationCurve blendCurve,
            CancellationToken token = default)
        {
            if (!clip)
            {
                return;
            }

            if (!_slotOf.TryGetValue(LayerInfo.LayerType.TopLayer, out var slot))
            {
                Debug.LogWarning("[AnimationClipPlayer] TopLayer が設定されていません。_layerInfo の最後に追加してください。");
                return;
            }

            if (!_runtimeClips.TryGetValue(LayerInfo.LayerType.TopLayer, out var currentClip)
                || !currentClip.IsValid())
            {
                PlayOnLayer(clip, LayerInfo.LayerType.TopLayer, speed);
                return;
            }

            var currentInput = _layerMixer.GetInput(slot);
            if (!currentInput.IsValid())
            {
                PlayOnLayer(clip, LayerInfo.LayerType.TopLayer, speed);
                return;
            }

            var nextClip = AnimationClipPlayable.Create(_graph, clip);
            nextClip.SetApplyFootIK(!IsFootIKDisabledFor(clip));
            nextClip.SetTime(0);
            nextClip.SetDuration(clip.length);
            nextClip.SetSpeed(speed);

            var transitionMixer = AnimationMixerPlayable.Create(_graph, 2);
            _layerMixer.DisconnectInput(slot);
            transitionMixer.ConnectInput(0, currentInput, 0);
            transitionMixer.ConnectInput(1, nextClip, 0);
            transitionMixer.SetInputWeight(0, 1f);
            transitionMixer.SetInputWeight(1, 0f);
            _layerMixer.ConnectInput(slot, transitionMixer, 0);
            _layerMixer.SetInputWeight(slot, 1f);
            var transitionRoot = _layerMixer.GetInput(slot);

            _runtimeClips[LayerInfo.LayerType.TopLayer] = nextClip;
            _clipOf[LayerInfo.LayerType.TopLayer] = clip;

            var blendDuration = Mathf.Max(blendTime, 0f);
            if (blendDuration <= 0f)
            {
                transitionMixer.SetInputWeight(0, 0f);
                transitionMixer.SetInputWeight(1, 1f);
                return;
            }

            try
            {
                float elapsed = 0f;
                while (elapsed < blendDuration)
                {
                    token.ThrowIfCancellationRequested();
                    elapsed += Time.deltaTime;
                    var progress = Mathf.Clamp01(elapsed / blendDuration);
                    if (blendCurve != null)
                    {
                        progress = Mathf.Clamp01(blendCurve.Evaluate(progress));
                    }

                    transitionMixer.SetInputWeight(0, 1f - progress);
                    transitionMixer.SetInputWeight(1, progress);
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }

                transitionMixer.SetInputWeight(0, 0f);
                transitionMixer.SetInputWeight(1, 1f);
            }
            catch (OperationCanceledException)
            {
                if (_runtimeClips.TryGetValue(LayerInfo.LayerType.TopLayer, out var current)
                    && current.Equals(nextClip)
                    && current.IsValid())
                {
                    _layerMixer.DisconnectInput(slot);
                    transitionRoot.DestroyTree();
                    _runtimeClips.Remove(LayerInfo.LayerType.TopLayer);
                    _clipOf.Remove(LayerInfo.LayerType.TopLayer);
                }

                throw;
            }
        }

        public void Play(AnimationClip clip, bool forcePlay = false)
        {
            if (!TryGetMontageIndex(clip, out int clipIndex))
            {
                return;
            }

            if (Application.isPlaying) RPC_Play(clipIndex, forcePlay);
            Play(clipIndex, forcePlay);
        }

        [Rpc(RpcSources.All, RpcTargets.All, InvokeLocal = false)]
        private void RPC_PlayAsync(int clipIndex)
        {
            PlayAsync(clipIndex).Forget();
        }

        private UniTask<EndClipType> PlayAsync(int clipIndex)
        {
            var montage = AnimationClipsContainer.Instance.AnimationMontages[clipIndex];
            return PlayAsync(montage.AnimClip, montage.TargetLayer, 1f, montage.BlendIn, montage.BlendOut,
                montage.IsAdditive, montage.PlaySpeed);
        }

        /// <summary>
        /// 指定のアニメーションを再生する。
        /// 開始前と終了前にWeightをBlend可能
        /// </summary>
        /// <param name="clip">再生するアニメーション</param>
        /// <param name="layerType">再生するレイヤーマスクの種類（ベースは不可）</param>
        /// <param name="weight">再生するレイヤーの重み</param>
        /// <param name="additive">加算モーションにするか</param>
        /// <param name="playSpeed">再生速度</param>
        /// <param name="external">外部から再生処理を止めるトークン。デフォルトではゲームオブジェクトのトークンに紐づく</param>
        /// <param name="blendIn">アニメーション再生開始時のブレンド</param>
        /// <param name="outBlend">アニメーション再生終了時のブレンド</param>
        private async UniTask<EndClipType> PlayAsync(
            AnimationClip clip,
            LayerInfo.LayerType layerType,
            float weight,
            LayerInfo.Blend blendIn,
            LayerInfo.Blend outBlend,
            bool additive = false,
            float playSpeed = 1f,
            CancellationToken external = default)
        {
            if (!clip) return EndClipType.Failed;
            if (layerType == LayerInfo.LayerType.Base)
            {
                Debug.LogWarning("Base レイヤーには PlayAsync() できません。");
                return EndClipType.Failed;
            }

            if (!_slotOf.TryGetValue(layerType, out int slot))
            {
                Debug.LogWarning($"未定義のレイヤー {layerType}");
                return EndClipType.Failed;
            }

            // 同レイヤーの前回待機をキャンセルして新トークン
            var token = RenewLayerCts(layerType, external);

            var currentW = Mathf.Clamp01(_layerInfo[slot].Weight);
            var useBlendIn = blendIn.BlendTime > 0f;
            var startW = useBlendIn ? currentW : Mathf.Clamp01(weight);
            var targetW = Mathf.Clamp01(weight);

            Play(clip, layerType, startW, additive, playSpeed);

            var liNow = _layerInfo[slot];
            liNow.Weight = startW;
            _layerInfo[slot] = liNow;

            // 再生中 Playable を取得
            if (!_runtimeClips.TryGetValue(layerType, out var played) || !played.IsValid()) return EndClipType.Failed;

            if (useBlendIn)
            {
                try
                {
                    await BlendWeightAsync(blendIn, token, startW, targetW, slot);
                }
                catch (OperationCanceledException)
                {
                    return EndClipType.Interrupted;
                }

                // 最終スナップ
                _layerMixer.SetInputWeight(slot, targetW);
                var liSnap = _layerInfo[slot];
                liSnap.Weight = targetW;
                _layerInfo[slot] = liSnap;
            }

            try
            {
                await WaitClipEndAsync(played, token);
            }
            catch (OperationCanceledException)
            {
                // キャンセル時：まだ自分（played）が刺さっている場合のみ片付け
                if (this != null && _graph.IsValid()
                                 && _runtimeClips.TryGetValue(layerType, out var stillCurrent)
                                 && stillCurrent.Equals(played) && stillCurrent.IsValid())
                {
                    SetInputWeight(slot, 0f);
                    DisconnectAndDestroy(layerType, stillCurrent, slot);
                }

                return EndClipType.Interrupted;
            }

            if (this == null || !_graph.IsValid()) return EndClipType.Interrupted;

            var from = Mathf.Clamp01(_layerInfo[slot].Weight);
            if (outBlend.BlendTime > 0f)
            {
                try
                {
                    await BlendWeightAsync(outBlend, token, from, 0, slot);
                }
                catch (OperationCanceledException)
                {
                    return EndClipType.Interrupted;
                }
            }

            // Out 完了時の最終スナップ → 0
            SetInputWeight(slot, 0f);

            // 0 になったら “まだ自分が current なら” 接続解除＆破棄
            DisconnectAndDestroy(layerType, played, slot);

            return EndClipType.Complete;
        }

        private void Play(int index, bool forcePlay = false)
        {
            var montage = AnimationClipsContainer.Instance.AnimationMontages[index];
            var layerType = montage.TargetLayer;
            var clip = montage.AnimClip;
            var playSpeed = montage.PlaySpeed;
            var additive = montage.IsAdditive;

            if (!forcePlay &&
                _runtimeClips.TryGetValue(layerType, out var playable) &&
                playable.IsValid() &&
                playable.GetAnimationClip() == clip)
            {
                playable.SetSpeed(playSpeed);
                return;
            }

            if (!_slotOf.TryGetValue(layerType, out int slot))
            {
                Debug.LogWarning($"未定義のレイヤー {layerType}");
                return;
            }

            RenewLayerCts(layerType);

            Play(clip, layerType, 1, additive, playSpeed);
        }

        [Rpc(RpcSources.All, RpcTargets.All, InvokeLocal = false)]
        private void RPC_Play(int index, bool forcePlay = false)
        {
            Play(index, forcePlay);
        }

        private void Play(AnimationClip clip, LayerInfo.LayerType layerType, float weight, bool additive = false, float playSpeed = 1f, bool loop = false)
        {
            if (!clip) return;

            if (layerType == LayerInfo.LayerType.Base)
            {
                Debug.LogWarning("Base レイヤーには Play() できません。");
                return;
            }

            if (!_slotOf.TryGetValue(layerType, out int slot))
            {
                Debug.LogWarning($"未定義のレイヤー {layerType}");
                return;
            }

            // 既存接続の後片付け
            if (_runtimeClips.TryGetValue(layerType, out var prev) && prev.IsValid())
            {
                DisconnectAndDestroy(layerType, prev, slot);
            }

            _clipOf[layerType] = clip;

            var p = AnimationClipPlayable.Create(_graph, clip);
            // Humanoid の Foot IK はクリップに焼かれた足位置へ補正し、リターゲットによる足滑りを防ぐ。
            // 意図的に切りたいクリップだけ AnimationClipsContainer 側で DisableFootIK を立てる。
            p.SetApplyFootIK(!IsFootIKDisabledFor(clip));
            p.SetTime(0);
            p.SetDuration(clip.length);
            p.SetSpeed(playSpeed);

            if (loop)
            {
                // AnimationClipPlayableの再生時間をループさせるPlayableを接続する
                // AnimationClipPlayable -> loopPlayable -> Mixer
                var loopPlayable = ScriptPlayable<LoopAnimationClipPlayableBehaviour>.Create(_graph, inputCount: 1);
                loopPlayable.GetBehaviour().AnimationClipPlayable = p;
                loopPlayable.ConnectInput(0, p, 0);
                _layerMixer.ConnectInput(slot, loopPlayable, 0);
            }
            else
            {
                _layerMixer.ConnectInput(slot, p, 0);
            }
            _layerMixer.SetLayerAdditive((uint)slot, additive);
            _layerMixer.SetInputWeight(slot, Mathf.Clamp01(weight));

            _runtimeClips[layerType] = p;
        }
        #endregion

        #region PlayLoop
        /// <summary>
        /// <see cref="AnimationClipsContainer"/>に登録されているMontageをループ再生します。
        /// 停止する場合は<see cref="StopClip"/>を使用してください。
        /// </summary>
        public void PlayClipLoop(AnimationClip clip)
        {
            if (!TryGetMontageIndex(clip, out int index))
            {
                Debug.LogWarning($"AnimationClip {clip.name} is not found in AnimationClipsContainer");
                return;
            }

            RPC_PlayLoop(index);
            PlayLoop(index);
        }

        [Rpc(RpcSources.All, RpcTargets.All, InvokeLocal = false)]
        private void RPC_PlayLoop(int clipIndex)
        {
            PlayLoop(clipIndex);
        }

        private void PlayLoop(int clipIndex)
        {
            var montage = AnimationClipsContainer.Instance.AnimationMontages[clipIndex];
            PlayLoop(montage.AnimClip, montage.TargetLayer, 1f, montage.BlendIn,
                montage.IsAdditive, montage.PlaySpeed).Forget();
        }

        /// <summary>
        /// 指定のアニメーションをループ再生する。
        /// </summary>
        /// <param name="clip">再生するアニメーション</param>
        /// <param name="layerType">再生するレイヤーマスクの種類（ベースは不可）</param>
        /// <param name="weight">再生するレイヤーの重み</param>
        /// <param name="additive">加算モーションにするか</param>
        /// <param name="playSpeed">再生速度</param>
        /// <param name="external">外部から再生処理を止めるトークン。デフォルトではゲームオブジェクトのトークンに紐づく</param>
        /// <param name="blendIn">アニメーション再生開始時のブレンド</param>
        private async UniTaskVoid PlayLoop(
            AnimationClip clip,
            LayerInfo.LayerType layerType,
            float weight,
            LayerInfo.Blend blendIn,
            bool additive = false,
            float playSpeed = 1f,
            CancellationToken external = default)
        {
            if (!clip) return;
            if (layerType == LayerInfo.LayerType.Base)
            {
                Debug.LogWarning("Base レイヤーには StartLoopAnimation() できません。");
                return;
            }

            if (!_slotOf.TryGetValue(layerType, out int slot))
            {
                Debug.LogWarning($"未定義のレイヤー {layerType}");
                return;
            }

            // 同レイヤーの前回待機をキャンセルして新トークン
            var token = RenewLayerCts(layerType, external);

            var currentW = Mathf.Clamp01(_layerInfo[slot].Weight);
            var useBlendIn = blendIn.BlendTime > 0f;
            var startW = useBlendIn ? currentW : Mathf.Clamp01(weight);
            var targetW = Mathf.Clamp01(weight);

            Play(clip, layerType, startW, additive, playSpeed, loop: true);

            var liNow = _layerInfo[slot];
            liNow.Weight = startW;
            _layerInfo[slot] = liNow;

            // 再生中 Playable を取得
            if (!_runtimeClips.TryGetValue(layerType, out var played) || !played.IsValid()) return;

            if (useBlendIn)
            {
                try
                {
                    await BlendWeightAsync(blendIn, token, startW, targetW, slot);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                // 最終スナップ
                _layerMixer.SetInputWeight(slot, targetW);
                var liSnap = _layerInfo[slot];
                liSnap.Weight = targetW;
                _layerInfo[slot] = liSnap;
            }
        }
        #endregion

        #region Weight
        public void SetLocoWeight(float w) => _locoWeight = w;

        public void SetLocoPlaybackRate(float rate)
        {
            rate = Mathf.Max(0f, rate);

            // 0番は待機、1番が歩き、2番が走り
            for (int i = 1; i < _baseMixer.GetInputCount(); i++)
            {
                var input = _baseMixer.GetInput(i);

                if (input.IsValid())
                    input.SetSpeed(rate);
            }
        }

        private void UpdateLocoBlend(float w)
        {
            w = Mathf.Clamp(w, 0f, 2f);
            float wWait, wWalk, wRun;
            if (w < 1f)
            {
                wWait = 1f - w;
                wWalk = w;
                wRun = 0f;
            }
            else
            {
                wWait = 0f;
                wWalk = 2f - w;
                wRun = w - 1f;
            }

            _baseMixer.SetInputWeight(0, wWait);
            _baseMixer.SetInputWeight(1, wWalk);
            _baseMixer.SetInputWeight(2, wRun);
        }

        public float GetTargetLayerWeight(LayerInfo.LayerType layer)
        {
            if (!_slotOf.TryGetValue(layer, out int slot))
            {
                Debug.LogWarning($"未定義のレイヤー {layer}");
                return 0f;
            }

            return _layerInfo[slot].Weight;
        }

        private void UpdateLayerBlendWeight(LayerInfo.LayerType layerType, LayerInfo.Blend blendIn, LayerInfo.Blend blendOut, float clipLength, float time)
        {
            LayerInfo.Blend blend;
            float startTime;

            {
                // イン
                if (time < blendIn.BlendTime)
                {
                    blend = blendIn;
                    startTime = 0;
                }
                // イン終了～アウト開始
                else if (time <= clipLength - blendOut.BlendTime)
                {
                    var w = blendIn.BlendCurve.Evaluate(1);
                    SetLayerWeight(layerType, w);
                    return;
                }
                // アウト
                else if (time > clipLength - blendOut.BlendTime)
                {
                    blend = blendOut;
                    startTime = clipLength - blendOut.BlendTime;
                }
                else
                {
                    SetLayerWeight(layerType, 0);
                    return;
                }
            }

            var blendDuration = Mathf.Max(blend.BlendTime, 1e-6f);
            var t = Mathf.Clamp01((time - startTime) / blendDuration);

            var weight = blend.BlendCurve.Evaluate(t);

            SetLayerWeight(layerType, weight);
        }

        private async UniTask BlendWeightAsync(LayerInfo.Blend outBlend, CancellationToken token, float from, float to,
            int slot)
        {
            float t = 0f, dur = Mathf.Max(outBlend.BlendTime, 1e-6f);
            var curve = outBlend.BlendCurve;
            while (t < dur)
            {
                token.ThrowIfCancellationRequested();
                t += Time.deltaTime;
                var a = Mathf.Clamp01(t / dur);
                if (curve != null) a = curve.Evaluate(a);
                var w = Mathf.Lerp(from, to, a);

                _layerMixer.SetInputWeight(slot, w);
                var liStep = _layerInfo[slot];
                liStep.Weight = w;
                _layerInfo[slot] = liStep;

                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }

        public async UniTask BlendLayerWeight(
            LayerInfo.LayerType layer,
            float toWeight,
            LayerInfo.Blend blend,
            CancellationToken external = default)
        {
            if (layer == LayerInfo.LayerType.Base)
            {
                Debug.LogWarning("Base レイヤーは SetLocoWeight() で制御してください。");
                return;
            }
            if (!_slotOf.TryGetValue(layer, out int slot))
            {
                Debug.LogWarning($"未定義のレイヤー {layer}");
                return;
            }

            // 進行中のブレンドをキャンセル
            if (_weightBlendCts.TryGetValue(layer, out var old))
            {
                old.Cancel();
                old.Dispose();
            }

            var linked = CancellationTokenSource.CreateLinkedTokenSource(
                external, this.GetCancellationTokenOnDestroy());
            _weightBlendCts[layer] = linked;
            var token = linked.Token;

            float from = Mathf.Clamp01(_layerInfo[slot].Weight);
            float to = Mathf.Clamp01(toWeight);

            if (Mathf.Approximately(blend.BlendTime, 0f))
            {
                SetLayerWeight(layer, to);
                linked.Dispose();
                _weightBlendCts.Remove(layer);
                return;
            }

            try
            {
                // 既存の補間ルーチンを利用（クラス内の private メソッド）
                await BlendWeightAsync(blend, token, from, to, slot);
            }
            catch (OperationCanceledException)
            {
                // キャンセル時はそのまま終了
                return;
            }
            finally
            {
                if (_weightBlendCts.TryGetValue(layer, out var cts))
                {
                    cts.Dispose();
                    _weightBlendCts.Remove(layer);
                }
            }

            SetLayerWeight(layer, to);
        }

        public void SetLayerWeight(LayerInfo.LayerType layer, float weight)
        {
            if (layer == LayerInfo.LayerType.Base)
            {
                Debug.LogWarning("Base レイヤーは SetLocoWeight を使ってください。");
                return;
            }
            if (!_slotOf.TryGetValue(layer, out int slot))
            {
                Debug.LogWarning($"未定義のレイヤー {layer}");
                return;
            }

            var w = Mathf.Clamp01(weight);
            _layerMixer.SetInputWeight(slot, w);      // Playables側に即反映
            var li = _layerInfo[slot];                // 内部状態も更新（Updateで毎フレーム再適用される）
            li.Weight = w;
            _layerInfo[slot] = li;
        }
        #endregion

        #region Destroy
        private void OnDisable() => SafeDestroy();
        private void OnDestroy() => SafeDestroy();

        public void SafeDestroy()
        {
            if (!_graph.IsValid()) return;

            foreach (var kv in _runtimeClips)
                if (kv.Value.IsValid())
                    kv.Value.Destroy();
            _runtimeClips.Clear();

            _graph.Destroy();
        }
        #endregion

        #region Utility
        /// <summary>
        /// アニメーションが再生中かどうかを判定します
        /// </summary>
        /// <param name="clip"></param>
        /// <param name="includeIsEnded">再生しきったアニメーションを判定に含めるか。持続モーションやループモーションを判定する時に使う。</param>
        /// <param name="includeZeroWeight">重みがゼロのアニメーションを判定に含めるか。ノードそのものの生存をチェックする時に使う。</param>
        public bool IsPlayingTargetClip(AnimationClip clip, bool includeIsEnded = false, bool includeZeroWeight = false)
        {
            foreach (var kv in _clipOf)
            {
                if (kv.Value == clip && _runtimeClips.TryGetValue(kv.Key, out var p) && p.IsValid())
                {
                    // レイヤー重みもチェック
                    if (_slotOf.TryGetValue(kv.Key, out int slot))
                    {
                        // 再生が完了していれば未再生判定（ワンショットモーションが再生完了後も持続しないようにするため）
                        if (!includeIsEnded && p.GetTime() >= p.GetDuration() - 0.01)
                        {
                            continue;
                        }

                        // Weightが0なら未再生判定。影響がない ＝ 再生していないものとしてあつかう
                        if (!includeZeroWeight && _layerMixer.GetInputWeight(slot) <= 0.001f)
                        {
                            continue;
                        }

                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// 以前使っていたレイヤーの処理が残っていればキャンセル
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="external"></param>
        /// <returns></returns>
        private CancellationToken RenewLayerCts(LayerInfo.LayerType layer, CancellationToken external = default)
        {
            if (_layerCts.TryGetValue(layer, out var old))
            {
                old?.Cancel();
                old?.Dispose();
            }

            var linked =
                CancellationTokenSource.CreateLinkedTokenSource(external, this.GetCancellationTokenOnDestroy());
            _layerCts[layer] = linked;
            return linked.Token;
        }

        private async UniTask WaitClipEndAsync(AnimationClipPlayable p, CancellationToken token)
        {
            const double EPS = 1e-4;
            while (true)
            {
                bool valid;
                try
                {
                    valid = p.IsValid();
                }
                catch
                {
                    break;
                } // 破棄レース保険

                if (!valid) break;

                double dur = 0, tim = 0;
                try
                {
                    dur = p.GetDuration();
                    tim = p.GetTime();
                }
                catch
                {
                    break;
                } // グラフ破棄直後の保険

                if (dur > 0 && tim + EPS >= dur) break;

                // キャンセル時に例外を投げない（Suppress）
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, token);
                if (token.IsCancellationRequested) break;
            }
        }

        /// <summary>
        /// 指定のPlayableが再生中であれば、Mixerとの接続を解除したのち破棄する
        /// </summary>
        private void DisconnectAndDestroy(LayerInfo.LayerType layerType, AnimationClipPlayable playable, int slot)
        {
            if (_runtimeClips.TryGetValue(layerType, out var current) && current.Equals(playable) && current.IsValid())
            {
                var input = _layerMixer.GetInput(slot);
                _layerMixer.DisconnectInput(slot);
                input.DestroyTree();
                _runtimeClips.Remove(layerType);
                _clipOf.Remove(layerType);
            }
        }

        /// <summary>
        /// AnimationClipsContainer で Foot IK を切る指定があるか。未登録クリップ (TopLayer 等) は既定で有効扱い。
        /// TryGetMontageIndex と違い、未登録でも警告を出さない。
        /// </summary>
        private static bool IsFootIKDisabledFor(AnimationClip clip)
        {
            var montages = AnimationClipsContainer.Instance?.AnimationMontages;
            if (montages == null) return false;

            int index = Array.FindIndex(montages, x => x.AnimClip && (x.AnimClip == clip || x.AnimClip.name == clip.name));
            return index >= 0 && montages[index].DisableFootIK;
        }

        private bool TryGetMontageIndex(AnimationClip clip, out int index)
        {
            index = -1;
            if (AnimationClipsContainer.Instance?.AnimationMontages == null)
            {
                Debug.LogWarning("AnimationClipsContainer Instance is null");
                return false;
            }

            index = Array.FindIndex(AnimationClipsContainer.Instance.AnimationMontages,
                x => x.AnimClip && (x.AnimClip == clip || x.AnimClip.name == clip.name));

            if (index < 0)
            {
                Debug.LogWarning($"AnimationClip {clip.name} is not found in AnimationClipsContainer", AnimationClipsContainer.Instance);
                return false;
            }

            return true;
        }
        #endregion

        #region StopClip
        public bool StopClip(AnimationClip clip)
        {
            if (AnimationClipsContainer.Instance.AnimationMontages == null)
            {
                return StopClipLocal(clip);
            }

            // AnimationClipsContainer から探してRPCで停止を全クライアントに通知
            var index = Array.FindIndex(AnimationClipsContainer.Instance.AnimationMontages,
                x => x.AnimClip && x.AnimClip.name == clip.name);

            if (index >= 0)
            {
                RPC_StopClip(index);
            }

            return StopClipLocal(index);
        }

        [Rpc(RpcSources.All, RpcTargets.All, InvokeLocal = false)]
        private void RPC_StopClip(int clipIndex)
        {
            StopClipLocal(clipIndex);
        }

        private bool StopClipLocal(int clipIndex)
        {
            var montage = AnimationClipsContainer.Instance.AnimationMontages[clipIndex];
            return StopClipLocal(montage.AnimClip, montage.BlendOut, destroyCancellationToken);
        }

        private bool StopClipLocal(AnimationClip clip, LayerInfo.Blend outBlend = default, CancellationToken token = default)
        {
            foreach ((LayerInfo.LayerType layer, AnimationClip playingClip) in _clipOf)
            {
                if (playingClip == clip && _runtimeClips.TryGetValue(layer, out var p) && p.IsValid())
                {
                    RenewLayerCts(layer);

                    _slotOf.TryGetValue(layer, out int slot);

                    if (outBlend.BlendTime > 0f)
                    {
                        try
                        {
                            StopClipAsync(slot, layer, outBlend, token).Forget();
                        }
                        catch (OperationCanceledException)
                        {
                            return false;
                        }
                    }
                    else
                    {
                        // レイヤーウェイトを0にリセット
                        SetInputWeight(slot, 0f);

                        if (_runtimeClips.TryGetValue(layer, out AnimationClipPlayable prev) && prev.IsValid())
                        {
                            DisconnectAndDestroy(layer, prev, slot);
                        }
                    }

                    return true;
                }
            }

            return false;
        }

        private async UniTaskVoid StopClipAsync(int slot, LayerInfo.LayerType layer,
            LayerInfo.Blend outBlend = default, CancellationToken token = default)
        {
            float from = Mathf.Clamp01(_layerInfo[slot].Weight);
            await BlendWeightAsync(outBlend, token, from, 0, slot);

            // 確実に0にする
            SetInputWeight(slot, 0f);

            if (_runtimeClips.TryGetValue(layer, out AnimationClipPlayable prev) && prev.IsValid())
            {
                DisconnectAndDestroy(layer, prev, slot);
            }
        }

        private void SetInputWeight(int slot, float weight)
        {
            _layerMixer.SetInputWeight(slot, weight);
            LayerInfo li = _layerInfo[slot];
            li.Weight = weight;
            _layerInfo[slot] = li;
        }

        #endregion

        #region Outside Controls
        public bool TryGetPlayableInfo(AnimationClip clip, out PlayableInfo info)
        {
            info = default;
            if (!TryGetMontageIndex(clip, out int index))
            {
                Debug.LogWarning($"AnimationClipPlayer: AnimationClip {clip.name} is not found in AnimationClipsContainer");
                return false;
            }
            var montage = AnimationClipsContainer.Instance.AnimationMontages[index];
            _runtimeClips.TryGetValue(montage.TargetLayer, out var playable);

            if (!playable.IsValid())
            {
                Debug.LogWarning($"AnimationClipPlayer: playable is not valid");
                return false;
            }

            var playableClip = playable.GetAnimationClip();
            if (playableClip != clip && playableClip.name != clip.name)
            {
                Debug.LogWarning($"AnimationClipPlayer: playable is not clip (playable:{playableClip?.name}, clip:{clip?.name})");
                return false;
            }

            info = new PlayableInfo(this, playable, montage, montage.AnimClip, montage.TargetLayer, _slotOf[montage.TargetLayer]);
            return true;
        }

        /// <summary>
        /// AnimationClipPlayerで管理しているPlayableを外部から操作するための型
        /// </summary>
        public readonly struct PlayableInfo
        {
            public readonly AnimationClipPlayer player;
            public readonly AnimationClipPlayable playable;
            public readonly AnimationMontageStruct montage;
            public readonly AnimationClip clip;
            public readonly LayerInfo.LayerType layerType;
            public readonly int slot;

            public PlayableInfo(AnimationClipPlayer player, AnimationClipPlayable playable, AnimationMontageStruct montage, AnimationClip clip, LayerInfo.LayerType layerType, int slot)
            {
                this.player = player;
                this.playable = playable;
                this.montage = montage;
                this.clip = clip;
                this.layerType = layerType;
                this.slot = slot;
            }

            public void SetTime(float time, bool updateBlendWeight = true)
            {
                playable.SetTime(time);
                playable.SetSpeed(0);

                if (updateBlendWeight)
                {
                    player.UpdateLayerBlendWeight(layerType, montage.BlendIn, montage.BlendOut, clip.length, time);
                }
            }

            public void SetBlendTime(float time, float clipLength)
            {
                player.UpdateLayerBlendWeight(layerType, montage.BlendIn, montage.BlendOut, clipLength, time);
            }

            /// <summary>
            /// このPlayableを削除する
            /// </summary>
            public void Disconnect()
            {
                if (!playable.IsValid()) return;
                player.SetLayerWeight(layerType, 0);
                player.DisconnectAndDestroy(layerType, playable, slot);
            }
        }
        #endregion
    }

    [Serializable]
    public class LayerInfo
    {
        public enum LayerType
        {
            Base = 0, //永続するモーション(移動など)
            FullBody = 1, //一時的な全身モーション
            UpperBody = 2,　//一時的な上半身モーション
            TopLayer = 3, //落下モーションレイヤ、最優先で再生される
        }

        public LayerType Type;
        public AvatarMask LayerMask;
        [Range(0, 1)] public float Weight = 0f;
        public bool Additive = false;

        [Serializable]
        public struct Blend
        {
            public float BlendTime;
            public AnimationCurve BlendCurve;
        }
    }

    public enum EndClipType
    {
        Failed,
        Complete,
        Interrupted
    }
}
