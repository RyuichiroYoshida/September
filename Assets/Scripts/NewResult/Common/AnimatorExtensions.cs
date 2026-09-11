using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Triggers;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

    public static class AnimatorExtensions
    {
        public static async UniTask PlayAsync(this Animator animator, string animationName, CancellationToken cancellationToken = default)
        {
            animator.Play(animationName);
            await UniTask.DelayFrame(1, cancellationToken: cancellationToken);
            await WaitUntilEndState(animator, animationName, cancellationToken);
        }

        public static async UniTask PlayAsync(this Animator animator, string animationName, int layer, float normalizedTime, CancellationToken cancellationToken = default)
        {
            animator.Play(animationName, layer, normalizedTime);
            await UniTask.DelayFrame(1, cancellationToken: cancellationToken);
            await WaitUntilEndState(animator, animationName, cancellationToken);
        }

        public static async UniTask WaitUntilEndState(this Animator animator, string stateName, CancellationToken cancellationToken = default)
        {
            await UniTask.WaitUntil(
                (animator, stateName), 
                static state =>
                {
                    var info = state.animator.GetCurrentAnimatorStateInfo(0);
                    return !info.IsName(state.stateName) || info.normalizedTime >= 1f;
                }, cancellationToken: cancellationToken);
        }

        public static async UniTask WaitState(this Animator animator, string stateName, CancellationToken cancellationToken = default)
        {
            await UniTask.WaitUntil((animator, stateName), c =>
            {
                var info = c.animator.GetCurrentAnimatorStateInfo(0);
                return info.IsName(c.stateName);
            }, cancellationToken: cancellationToken);
        }

        public static bool IsPlaying(this Animator animator, string stateName)
        {
            return !string.IsNullOrEmpty(stateName) && animator.GetCurrentAnimatorStateInfo(0).IsName(stateName);
        }

        /// <summary>
        /// アニメーションクリップを再生します。
        /// 他のアニメーション処理との併用はしないでください。
        /// ループが有効なアニメーションクリップはループ再生し続けます。
        /// </summary>
        public static void PlayInstant(this Animator animator, AnimationClip clip)
        {
            PlayInstantAsync(animator, clip).Forget();
        }
        
        private static async UniTaskVoid PlayInstantAsync(this Animator animator, AnimationClip clip)
        {
            var graph = PlayableGraph.Create();
            var output = AnimationPlayableOutput.Create(graph, string.Empty, animator);
            
            var playable = AnimationClipPlayable.Create(graph, clip);
            if (!clip.isLooping) playable.SetDuration(clip.length);
            
            output.SetSourcePlayable(playable);
            graph.Play();

            await UniTask.WhenAny(animator.OnDestroyAsync(), UniTask.WaitUntil(playable, p => p.IsDone()));
            
            graph.Destroy();
        }
        
        public static string ToFieldName(this string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            value = value.TrimStart('_');
            value = char.ToUpper(value[0]) + value[1..];
            return value;
        }
    }
