using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.Playables;

namespace Common.Extensions
{
    public static class PlayableExtensions
    {
        public static async UniTask WaitUntilEnd(this PlayableDirector playableDirector, CancellationToken token = default) 
        {
            await UniTask.WaitUntil(playableDirector, p => p.state != PlayState.Playing, cancellationToken: token);
        }

        public static async UniTask PlayAsync(this PlayableDirector playableDirector, CancellationToken token = default)
        {
            playableDirector.Play();

            try
            {
                await playableDirector.WaitUntilEnd(token);
            }
            finally
            {
                if (token.IsCancellationRequested && playableDirector)
                {
                    playableDirector.Stop();
                }
            }
        }

        public static async UniTask PlayAsync(this PlayableDirector playableDirector, PlayableAsset asset, CancellationToken token = default)
        {
            playableDirector.Play(asset);

            try
            {
                await playableDirector.WaitUntilEnd(token);
            }
            finally
            {
                if (token.IsCancellationRequested && playableDirector)
                {
                    playableDirector.Stop();
                }
            }
        }

        /// <summary>
        /// 自身に接続されている全てのノードを再帰的に削除します。
        /// </summary>
        public static void DestroyTree(this Playable root, bool destroySelf = true)
        {
            int count = root.GetInputCount();
            for (int i = 0; i < count; i++)
            {
                Playable child = root.GetInput(i);
                if (!child.IsValid()) continue;

                if (child.GetInputCount() > 0)
                {
                    DestroyTree(child);
                }

                child.Destroy();
            }

            if (destroySelf) root.Destroy();
        }
    }
}
