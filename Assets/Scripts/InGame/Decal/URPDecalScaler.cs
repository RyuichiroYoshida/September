using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace September.InGame.Decal
{
    /// <summary>
    /// ScaleMode を InheritFromHierarchy にしたときに AngleFade の動作が不安定になるので、
    /// ScaleInvariant のままで同じように動作させるやつ
    /// </summary>
    [RequireComponent(typeof(DecalProjector))]
    [ExecuteAlways]
    public class URPDecalScaler : MonoBehaviour
    {
        private DecalProjector _decalProjector;

        private void Start()
        {
            _decalProjector = GetComponent<DecalProjector>();
        }

        private void Update()
        {
            _decalProjector.size = transform.lossyScale;
        }
    }
}
