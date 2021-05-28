using System;
namespace UnityEngine.Experiemntal.Rendering.Universal
{
    [RequireComponent(typeof(Renderer))]
    [RequireComponent(typeof(MeshFilter))]
    public class PixelProjectedReflectionPlane : MonoBehaviour
    {
        [Range(0, 2f)]
        public float PreFilterValue;

        public bool DualBlur;

        private void OnEnable()
        {
            GetComponent<Renderer>().renderingLayerMask = 2;
        }
        
        
    }
}