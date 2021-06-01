using System;
namespace UnityEngine.Experiemntal.Rendering.Universal
{
    [RequireComponent(typeof(Renderer))]
    [RequireComponent(typeof(MeshFilter))]
    public class PixelProjectedReflectionPlane : MonoBehaviour
    {
        [Range(0, 3f)]
        public float PreFilterValue;
        [Tooltip("随反射距离变化的模糊")]
        public bool AdaptiveBlur;
        [Tooltip("两次模糊处理")]
        public bool DualBlur;
        [Range(0, 30f), Tooltip("达到最强模糊需要的距离")]
        public float BlurMaxDistance;
    }
}