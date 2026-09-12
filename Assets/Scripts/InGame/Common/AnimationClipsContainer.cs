using System;
using UnityEngine;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace InGame.Common
{
    // AnimationClipPlayer で使用する AnimationClip 共有用
    [CreateAssetMenu(fileName = "AnimationClipsContainer", menuName = "Scriptable Objects/AnimationClipsContainer")]
    public class AnimationClipsContainer : ScriptableObject
    {
        private const string AssetPath = "AnimationClipsContainer";
        
        [SerializeField] private AnimationMontageStruct[] _animationMontages;
        
        public static AnimationClipsContainer Instance { get; private set; }
        public AnimationMontageStruct[] AnimationMontages => _animationMontages;

        #if UNITY_EDITOR
        [InitializeOnLoadMethod]
        #else
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        #endif
        static void Init()
        {
            try
            {
                var handle = Addressables.LoadAssetAsync<AnimationClipsContainer>(AssetPath);
                Instance = handle.WaitForCompletion();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }

    [Serializable]
    public struct AnimationMontageStruct
    {
        public AnimationClip AnimClip;
        public float PlaySpeed;
        [Header("Blend")]
        public LayerInfo.Blend BlendIn;
        public LayerInfo.Blend BlendOut;
        
        [Header("Layer Meta")]
        public LayerInfo.LayerType TargetLayer;
        public bool IsAdditive;
        [Tooltip("ON にすると Humanoid の Foot IK (クリップに焼かれた足位置への補正) を切る。通常は OFF のままで、リターゲット時の足滑りを防ぐ")]
        public bool DisableFootIK;

        public AnimationMontageStruct(float playSpeed = 1)
        {
            AnimClip = null;
            PlaySpeed = playSpeed;
            BlendIn = new LayerInfo.Blend();
            BlendOut = new LayerInfo.Blend();
            TargetLayer = LayerInfo.LayerType.Base;
            IsAdditive = false;
            DisableFootIK = false;
        }
    }
}
