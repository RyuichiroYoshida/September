using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class PlayerAvatorAnimationEventReceiver : MonoBehaviour
{
    [SerializeField] AudioClip[] foorAudioClips;
    
    [SerializeField] AudioClip landingAudioClip;

    private void OnFootStep(AnimationEvent animationEvent)
    {
        if (animationEvent.animatorClipInfo.weight > 0.5f)
        {
            var index = Random.Range(0, foorAudioClips.Length);
            AudioSource.PlayClipAtPoint(foorAudioClips[index], transform.position);
        }
    }
    
    private void OnLand(AnimationEvent animationEvent)
    {
        if (animationEvent.animatorClipInfo.weight > 0.5f)
        {
            AudioSource.PlayClipAtPoint(landingAudioClip, transform.position);
        }
    }
}
