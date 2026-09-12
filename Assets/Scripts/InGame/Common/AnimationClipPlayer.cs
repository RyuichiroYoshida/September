using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Fusion;
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
        [Header("Aimアニメーション")]
        [SerializeField] private AnimationClip _aimWait;
        [SerializeField] private AnimationClip _aimFrontWalk;
        [SerializeField] private AnimationClip _aimBackWalk;
        [SerializeField] private AnimationClip _aimRightWalk;
        [SerializeField] private AnimationClip _aimLeftWalk;
        [SerializeField, Range(0f, 2f)] private float _locoWeight = 0f;
        [SerializeField] protected Animator _animator;

        private PlayableGraph _graph;
        private AnimationMixerPlayable _baseMixer; // 通常とAimをまとめるMixer
        private AnimationMixerPlayable _normalMixer; // 通常
        private AnimationMixerPlayable _aimMixer; // Aim
        private AnimationLayerMixerPlayable _layerMixer;

        /// <summary>
        /// LayerMixerに登録しているInputSlotのindex
        /// </summary>
        private readonly Dictionary<LayerInfo.LayerType, int> _slotOf = new();
        private readonly Dictionary<LayerInfo.LayerType, AnimationClipPlayable> _runtimeClips = new();
        private readonly Dictionary<LayerInfo.LayerType, CancellationTokenSource> _layerCts = new();
        private readonly Dictionary<LayerInfo.LayerType, CancellationTokenSource> _weightBlendCts = new();

        private readonly Dictionary<LayerInfo.LayerType, AnimationClip> _clipOf = new();
        
        // NormalMixerに接続されている各移動アニメーション
        private AnimationClipPlayable _waitClipPlayable;
        private AnimationClipPlayable _walkClipPlayable;
        private AnimationClipPlayable _runClipPlayable;
        // AimMixerに接続されている各Aimアニメーション
        private AnimationClipPlayable _aimWaitClipPlayable;
        private AnimationClipPlayable _aimFrontWalkClipPlayable;
        private AnimationClipPlayable _aimBackWalkClipPlayable;
        private AnimationClipPlayable _aimRightWalkClipPlayable;
        private AnimationClipPlayable _aimLeftWalkClipPlayable;
        // 入力ポート（通常）
        private int _waitPort;
        private int _walkPort;
        private int _runPort;

        public AnimationMixerPlayable BaseMixer => _baseMixer;
        public AnimationMixerPlayable NormalMixer => _normalMixer;
        public AnimationMixerPlayable AimMixer => _aimMixer;

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

            var output = AnimationPlayableOutput.Create(_graph, "AnimationOutput", _animator);
            output.SetWeight(1f);

            _layerMixer = AnimationLayerMixerPlayable.Create(_graph, _layerInfo.Count);
            output.SetSourcePlayable(_layerMixer);
            
            BaseMixerInitialize();
            NormalMixerInitialize();
            AimMixerInitialize();
            BaseMixerConnect();
            
            // 開始時は通常にする
            _baseMixer.SetInputWeight(0, 1f);
            _baseMixer.SetInputWeight(1, 0f);
            _graph.Play();
        }

        /// <summary>
        /// BaseMixer初期化
        /// </summary>
        private void BaseMixerInitialize()
        {
            _baseMixer = AnimationMixerPlayable.Create(_graph, 2);
            
            var baseSlot = _slotOf[LayerInfo.LayerType.Base];
            _graph.Connect(_baseMixer, 0, _layerMixer, baseSlot);
            _layerMixer.SetInputWeight(baseSlot, 1f);
            _layerMixer.SetLayerAdditive((uint)baseSlot, false);
            
            // 各レイヤーの初期設定
            for (int i = 0; i < _layerInfo.Count; i++)
            {
                var li = _layerInfo[i];
                if (li.LayerMask) _layerMixer.SetLayerMaskFromAvatarMask((uint)i, li.LayerMask);
                _layerMixer.SetLayerAdditive((uint)i, li.Additive);
                if (i != baseSlot) _layerMixer.SetInputWeight(i, Mathf.Clamp01(li.Weight));
            }
        }

        /// <summary>
        /// NormalMixer初期化
        /// </summary>
        private void NormalMixerInitialize()
        {
            _normalMixer = AnimationMixerPlayable.Create(_graph, 3);
            
            var port = 0;
            if (_wait)
            {
                _waitPort = port;
                _waitClipPlayable = AnimationClipPlayable.Create(_graph, _wait);
                _normalMixer.ConnectInput(port++, _waitClipPlayable, 0);
            }
            else _normalMixer.SetInputWeight(port++, 0f);

            if (_walk)
            {
                _walkPort = port;
                _walkClipPlayable = AnimationClipPlayable.Create(_graph, _walk);
                _normalMixer.ConnectInput(port++, _walkClipPlayable, 0);
            }
            else _normalMixer.SetInputWeight(port++, 0f);

            if (_run)
            {
                _runPort = port;
                _runClipPlayable = AnimationClipPlayable.Create(_graph, _run);
                _normalMixer.ConnectInput(port, _runClipPlayable, 0);
            }
            else _normalMixer.SetInputWeight(port, 0f);
        }

        /// <summary>
        /// AimMixer初期化
        /// </summary>
        private void AimMixerInitialize()
        {
            _aimMixer = AnimationMixerPlayable.Create(_graph, 5);
            
            var port = 0;
            if (_aimWait)
            {
                _aimWaitClipPlayable = AnimationClipPlayable.Create(_graph, _aimWait);
                _aimMixer.ConnectInput(port++, _aimWaitClipPlayable, 0);
            }
            else _aimMixer.SetInputWeight(port++, 0f);

            if (_aimFrontWalk)
            {
                _aimFrontWalkClipPlayable = AnimationClipPlayable.Create(_graph, _aimFrontWalk);
                _aimMixer.ConnectInput(port++, _aimFrontWalkClipPlayable, 0);
            }
            else _aimMixer.SetInputWeight(port++, 0f);

            if (_aimBackWalk)
            {
                _aimBackWalkClipPlayable = AnimationClipPlayable.Create(_graph, _aimBackWalk);
                _aimMixer.ConnectInput(port++, _aimBackWalkClipPlayable, 0);
            }
            else _aimMixer.SetInputWeight(port++, 0f);
            
            if (_aimRightWalk)
            {
                _aimRightWalkClipPlayable = AnimationClipPlayable.Create(_graph, _aimRightWalk);
                _aimMixer.ConnectInput(port++, _aimRightWalkClipPlayable, 0);
            }
            else _aimMixer.SetInputWeight(port++, 0f);
            
            if (_aimLeftWalk)
            {
                _aimLeftWalkClipPlayable = AnimationClipPlayable.Create(_graph, _aimLeftWalk);
                _aimMixer.ConnectInput(port, _aimLeftWalkClipPlayable, 0);
            }
            else _aimMixer.SetInputWeight(port, 0f);
        }

        /// <summary>
        /// BaseMixerへと接続
        /// </summary>
        private void BaseMixerConnect()
        {
            _graph.Connect(_normalMixer, 0, _baseMixer, 0);
            _graph.Connect(_aimMixer, 0, _baseMixer, 1);
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
            _graph.Evaluate(Time.deltaTime * _graphSpeed);
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
        public void PlayOnTopLayer(AnimationClip clip)
        {
            if (!_slotOf.TryGetValue(LayerInfo.LayerType.TopLayer, out var slot))
            {
                Debug.LogWarning("[AnimationClipPlayer] TopLayer が設定されていません。_layerInfo の最後に追加してください。");
                return;
            }

            // 解除要求
            if (clip == null)
            {
                _layerMixer.SetInputWeight(slot, 0f);

                if (_runtimeClips.TryGetValue(LayerInfo.LayerType.TopLayer, out var current) && current.IsValid())
                {
                    _layerMixer.DisconnectInput(slot);
                    current.Destroy();
                    _runtimeClips.Remove(LayerInfo.LayerType.TopLayer);
                }

                var li0 = _layerInfo[slot];
                li0.Weight = 0f;
                _layerInfo[slot] = li0;
                return;
            }

            if (_runtimeClips.TryGetValue(LayerInfo.LayerType.TopLayer, out var prev) && prev.IsValid())
            {
                _layerMixer.DisconnectInput(slot);
                prev.Destroy();
                _runtimeClips.Remove(LayerInfo.LayerType.TopLayer);
            }

            Play(clip, LayerInfo.LayerType.TopLayer, 1f, additive: false);

            var li = _layerInfo[slot];
            li.Weight = 1f; // Update() で毎フレーム反映されるので内部Weightも更新
            _layerInfo[slot] = li;
        }

        /// <summary>
        /// UpperBodyでアニメーションを再生
        /// </summary>
        /// <param name="clip">再生するアニメーション（clipがnullのときはアニメーションを解除する）</param>
        public void PlayOnUpperBody(AnimationClip clip)
        {
            if (Object.HasStateAuthority)
            {
                var index = -1;
                if (clip != null && TryGetMontageIndex(clip, out var clipIndex))
                {
                    index = clipIndex;
                }
                // クライアント側で再生する
                RPC_PlayOnUpperBody(index);
            }

            // ホスト側で再生する
            ExecutePlayOnUpperBodyInternal(clip);
        }
        
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_PlayOnUpperBody(int clipIndex)
        {
            AnimationClip clip = null;

            // 範囲内か判定を行う
            var montages = AnimationClipsContainer.Instance.AnimationMontages;
            if (clipIndex >= 0)
            {
                clip = montages[clipIndex].AnimClip;
            }

            // クライアント側で再生する
            ExecutePlayOnUpperBodyInternal(clip);
        }
        
        /// <summary>
        /// UpperBodyでアニメーションを再生、解除を行う
        /// </summary>
        /// <param name="clip">再生するアニメーション（clipがnullのときはアニメーションを解除する）</param>
        private void ExecutePlayOnUpperBodyInternal(AnimationClip clip)
        {
            if (!_slotOf.TryGetValue(LayerInfo.LayerType.UpperBody, out var slot))
            {
                Debug.LogWarning("[AnimationClipPlayer] UpperBody が設定されていません。_layerInfo の最後に追加してください。");
                return;
            }

            // 解除要求
            if (clip == null)
            {
                _layerMixer.SetInputWeight(slot, 0f);

                if (_runtimeClips.TryGetValue(LayerInfo.LayerType.UpperBody, out var current) && current.IsValid())
                {
                    _layerMixer.DisconnectInput(slot);
                    current.Destroy();
                    _runtimeClips.Remove(LayerInfo.LayerType.UpperBody);
                }

                var li0 = _layerInfo[slot];
                li0.Weight = 0f;
                _layerInfo[slot] = li0;
                return;
            }

            if (_runtimeClips.TryGetValue(LayerInfo.LayerType.UpperBody, out var prev) && prev.IsValid())
            {
                _layerMixer.DisconnectInput(slot);
                prev.Destroy();
                _runtimeClips.Remove(LayerInfo.LayerType.UpperBody);
            }

            Play(clip, LayerInfo.LayerType.UpperBody, 1f, additive: false);

            var li = _layerInfo[slot];
            li.Weight = 1f; // Update() で毎フレーム反映されるので内部Weightも更新
            _layerInfo[slot] = li;
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
                montage.IsAdditive, montage.PlaySpeed, montage._loop);
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
            bool loop = false,
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
                if (!loop) // ループしないアニメーションの場合
                {
                    // 再生時間が終了するまで待機する
                    await WaitClipEndAsync(played, token);
                }
                else // ループするアニメーションの場合
                {
                    // 構えなどの継続するアニメーション用
                    // 再生時間の終了を待たずに、StopClipが呼ばれるまで待機する
                    while (!token.IsCancellationRequested)
                    {
                        await UniTask.Yield(PlayerLoopTiming.Update);
                    }

                    return EndClipType.Interrupted;
                }
            }
            catch (OperationCanceledException)
            {
                // キャンセル時：まだ自分（played）が刺さっている場合のみ片付け
                if (this != null && _graph.IsValid()
                                 && _runtimeClips.TryGetValue(layerType, out var stillCurrent)
                                 && stillCurrent.Equals(played) && stillCurrent.IsValid())
                {
                    _layerMixer.SetInputWeight(slot, 0f);
                    _layerMixer.DisconnectInput(slot);
                    stillCurrent.Destroy();
                    _runtimeClips.Remove(layerType);

                    var li = _layerInfo[slot];
                    li.Weight = 0f;
                    _layerInfo[slot] = li;
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
            _layerMixer.SetInputWeight(slot, 0f);
            var liOut = _layerInfo[slot];
            liOut.Weight = 0f;
            _layerInfo[slot] = liOut;

            // 0 になったら “まだ自分が current なら” 接続解除＆破棄
            Disconnect(layerType, played, slot);

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

        private void Play(AnimationClip clip, LayerInfo.LayerType layerType, float weight, bool additive = false, float playSpeed = 1f)
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
                _layerMixer.DisconnectInput(slot);
                prev.Destroy();
            }

            _clipOf[layerType] = clip;

            var p = AnimationClipPlayable.Create(_graph, clip);
            p.SetApplyFootIK(false);
            p.SetTime(0);
            p.SetDuration(clip.length);
            p.SetSpeed(playSpeed);

            _layerMixer.ConnectInput(slot, p, 0);
            _layerMixer.SetLayerAdditive((uint)slot, additive);
            _layerMixer.SetInputWeight(slot, Mathf.Clamp01(weight));

            _runtimeClips[layerType] = p;
        }
        #endregion

        #region Weight
        public void SetLocoWeight(float w) => _locoWeight = w;

        public void SetLocoPlaybackRate(float rate)
        {
            rate = Mathf.Max(0, rate);
            SetMixerPlaybackRate(_normalMixer, rate);
            SetMixerPlaybackRate(_aimMixer, rate);
        }

        /// <summary>
        /// Mixerに接続されているアニメーションの再生速度を変更
        /// </summary>
        /// <param name="mixer">変更するMixer</param>
        /// <param name="rate">再生速度倍率</param>
        private void SetMixerPlaybackRate(AnimationMixerPlayable mixer, float rate)
        {
            // 0は待機のため、1から開始
            for (int i = 1; i < mixer.GetInputCount(); i++)
            {
                var input = mixer.GetInput(i);

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
            
            SetLocoBlendWeight(_normalMixer, wWait, wWalk, wRun);
        }

        /// <summary>
        /// Mixerに接続されている各アニメーションのWeightを設定
        /// </summary>
        /// <param name="mixer">設定するMixer</param>
        /// <param name="wait">待機</param>
        /// <param name="walk">歩き</param>
        /// <param name="run">走り</param>
        private void SetLocoBlendWeight(AnimationMixerPlayable mixer, float wait, float walk, float run)
        {
            mixer.SetInputWeight(0, wait);
            mixer.SetInputWeight(1, walk);
            mixer.SetInputWeight(2, run);
        }

        /// <summary>
        /// AimMixerに接続されている各AimアニメーションのWeightを設定
        /// </summary>
        /// <param name="move">入力</param>
        public void SetAimLocoBlendWeight(Vector2 move)
        {
            move = Vector2.ClampMagnitude(move, 1f);

            var front = Mathf.Max(0, move.y);
            var back = Mathf.Max(0, -move.y);
            var right =  Mathf.Max(0, move.x);
            var left =  Mathf.Max(0, -move.x);
            // 移動量によって減少
            var wait = Mathf.Clamp01(1 - Mathf.Max(Mathf.Abs(move.x), Mathf.Abs(move.y)));
            
            // 合計値で割って、割合を求める
            float total = wait + front + back + right + left;
            if(total > 0)
            {
                wait   /= total;
                front  /= total;
                back   /= total;
                right  /= total;
                left   /= total;
            }
            
            // Weight設定
            _aimMixer.SetInputWeight(0, wait);
            _aimMixer.SetInputWeight(1, front);
            _aimMixer.SetInputWeight(2, back);
            _aimMixer.SetInputWeight(3, right);
            _aimMixer.SetInputWeight(4, left);
        }

        /// <summary>
        /// Aim設定
        /// </summary>
        /// <param name="aim">true：Aimアニメーション　false：通常アニメーション</param>
        public void SetAim(bool aim)
        {
            if (aim) // Aimアニメーションに変更
            {
                _baseMixer.SetInputWeight(0, 0f);
                _baseMixer.SetInputWeight(1, 1f);
            }
            else // 通常アニメーションに変更
            {
                _baseMixer.SetInputWeight(0, 1f);
                _baseMixer.SetInputWeight(1, 0f);
            }
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
        public bool IsPlayingTargetClip(AnimationClip clip)
        {
            foreach (var kv in _clipOf)
            {
                if (kv.Value == clip && _runtimeClips.TryGetValue(kv.Key, out var p) && p.IsValid())
                {
                    // レイヤー重みもチェック
                    if (_slotOf.TryGetValue(kv.Key, out int slot) && _layerMixer.GetInputWeight(slot) > 0.001f)
                    {
                        // アニメーション完了状態もチェック
                        if (p.GetTime() < p.GetDuration() - 0.01) // まだ再生中
                        {
                            return true;
                        }
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
        private void Disconnect(LayerInfo.LayerType layerType, AnimationClipPlayable playable, int slot)
        {
            if (_runtimeClips.TryGetValue(layerType, out var current) && current.Equals(playable) && current.IsValid())
            {
                _layerMixer.DisconnectInput(slot);
                current.Destroy();
                _runtimeClips.Remove(layerType);
                _clipOf.Remove(layerType);
            }
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
                x => x.AnimClip.name == clip.name);

            if (index >= 0)
            {
                RPC_StopClip(index);
            }

            return StopClipLocal(clip);
        }

        [Rpc(RpcSources.All, RpcTargets.All)]
        private void RPC_StopClip(int clipIndex)
        {
            var montage = AnimationClipsContainer.Instance.AnimationMontages[clipIndex];
            StopClipLocal(montage.AnimClip);
        }

        private bool StopClipLocal(AnimationClip clip)
        {
            foreach (var kv in _clipOf)
            {
                if (kv.Value == clip && _runtimeClips.TryGetValue(kv.Key, out var p) && p.IsValid())
                {
                    RenewLayerCts(kv.Key);

                    _slotOf.TryGetValue(kv.Key, out int slot);

                    // レイヤーウェイトを0にリセット
                    _layerMixer.SetInputWeight(slot, 0f);
                    var li = _layerInfo[slot];
                    li.Weight = 0f;
                    _layerInfo[slot] = li;

                    if (_runtimeClips.TryGetValue(kv.Key, out var prev) && prev.IsValid())
                    {
                        _layerMixer.DisconnectInput(slot);
                        prev.Destroy();
                    }

                    _clipOf.Remove(kv.Key);
                    _runtimeClips.Remove(kv.Key);

                    return true;
                }
            }

            return false;
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
                if(!playable.IsValid()) return;
                
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
                player.Disconnect(layerType, playable, slot);
            }
        }
        #endregion

        #region ChangeAnimationClip

        /// <summary>
        /// 待機アニメーションを別のアニメーションに変更する
        /// </summary>
        /// <param name="clip">変更後のアニメーション</param>
        public void ChangeWaitAnimationClip(AnimationClip clip)
        {
            // 現在の待機アニメーションを切断
            _normalMixer.DisconnectInput(_waitPort);
            
            // 古いアニメーションを破棄し、変更後のアニメーションを作成し接続
            _waitClipPlayable.Destroy();
            _waitClipPlayable = AnimationClipPlayable.Create(_graph, clip);
            _normalMixer.ConnectInput(_waitPort, _waitClipPlayable, 0);
        }

        /// <summary>
        /// 歩きアニメーションを別のアニメーションに変更する
        /// </summary>
        /// <param name="clip">変更後のアニメーション</param>
        public void ChangeWalkAnimationClip(AnimationClip clip)
        {
            _normalMixer.DisconnectInput(_walkPort);
            
            _walkClipPlayable.Destroy();
            _walkClipPlayable = AnimationClipPlayable.Create(_graph, clip);
            _normalMixer.ConnectInput(_walkPort, _walkClipPlayable, 0);
        }

        /// <summary>
        /// 走りアニメーションを別のアニメーションに変更する
        /// </summary>
        /// <param name="clip">変更後のアニメーション</param>
        public void ChangeRunAnimationClip(AnimationClip clip)
        {
            _normalMixer.DisconnectInput(_runPort);
            
            _runClipPlayable.Destroy();
            _runClipPlayable = AnimationClipPlayable.Create(_graph, clip);
            _normalMixer.ConnectInput(_runPort, _runClipPlayable, 0);
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
