using UnityEngine.Animations;
using UnityEngine.Playables;

namespace September.InGame.Common
{
    /// <summary>
    /// Inputに接続されたAnimationClipPlayableをループ再生します
    /// </summary>
    public class LoopAnimationClipPlayableBehaviour : PlayableBehaviour
    {
        public AnimationClipPlayable AnimationClipPlayable;

        public override void PrepareFrame(Playable playable, FrameData info)
        {
            double duration = AnimationClipPlayable.GetDuration();
            double time = playable.GetTime();

            AnimationClipPlayable.SetTime(time % duration);
        }
    }
}
