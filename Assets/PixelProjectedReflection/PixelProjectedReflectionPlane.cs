using System;
namespace UnityEngine.Experiemntal.Rendering.Universal
{
    [RequireComponent(typeof(Renderer))]
    [RequireComponent(typeof(MeshFilter))]
    public class PixelProjectedReflectionPlane : MonoBehaviour
    {
        [Range(-1, 1)]
        public float Offset;
        [Range(0, 3f), Tooltip("预处理模糊强度值")]
        public float PreFilterValue;
        [Tooltip("两次模糊处理")]
        public bool DualBlur;
        [Tooltip("随反射距离变化的模糊")]
        public bool AdaptiveBlur;
        [Range(0, 30f), Tooltip("开启AdaptiveBlur时，达到最强模糊需要的距离")]
        public float BlurMaxDistance;
    }
}