using Fusion;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace InGame.Player.Hatano
{
    public class HatanoSequenceManager : NetworkBehaviour
    {
        [SerializeField] private PlayableDirector _director;
        [SerializeField] private TimelineAsset _startTimeline;
        [SerializeField] private TimelineAsset _endTimeline;
        
        public bool IsSequencePlaying()
        {
            return _director.state == PlayState.Playing;
        }
        
        [Rpc]
        public void RPC_SetEndTimeline()
        {
            _director.playableAsset = _endTimeline;
            _director.Play();
        }

        [Rpc]
        public void RPC_SetStartTimeline()
        {
            _director.playableAsset = _startTimeline;
        }
    }
}
