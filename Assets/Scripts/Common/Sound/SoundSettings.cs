using UnityEngine;

namespace CRISound
{
    [CreateAssetMenu(fileName = "SoundSettings")]
    public class SoundSettings : ScriptableObject
    {
        public string SoundName;
        public float Volume;
        public bool IsLoop;
    }
}