using Fusion;
using UnityEngine;

namespace InGame.Player
{
    public class PlayerRenderer : NetworkBehaviour
    {
        [SerializeField] private Material _opticalCamouflageMaterial;
        [SerializeField] private Renderer[] _renderers;
        private Material[][] _defaultMaterials;
        public void Awake()
        {
            _defaultMaterials = new Material[_renderers.Length][];
            for (int i = 0; i < _renderers.Length; i++)
            {
                _defaultMaterials[i] = _renderers[i].materials;
            }
        }
        [Rpc]
        public void Rpc_SetOpticalCamouflageMaterial()
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                var mats = _renderers[i].materials;
                for (int j = 0; j < mats.Length; j++)
                {
                    mats[j] = _opticalCamouflageMaterial;
                }
                _renderers[i].materials = mats;
            }
        }
        [Rpc]
        public void Rpc_ResetMaterial()
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                _renderers[i].materials = _defaultMaterials[i];
            }
        }
        [Rpc]
        public void Rpc_SetNullMaterial()
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                _renderers[i].materials = null;
            }
        }
    }
}