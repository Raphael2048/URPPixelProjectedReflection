using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UnityEngine.Experimental.Rendering.Universal
{
    public class PixelProjectedReflectionFeature : ScriptableRendererFeature
    {
        [Serializable]
        public class Settings
        {
            public ComputeShader projection;
            public Shader reflection;
            public Shader filter;
        }

        public Settings _settings;
        
        private PixelProjectedReflectionPass _pass;
        public override void Create()
        {
            _pass = new PixelProjectedReflectionPass(_settings);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            // Debug.Log(renderer.cameraDepth);
            _pass.Setup(renderer.cameraColorTarget, renderer.cameraDepth);
            renderer.EnqueuePass(_pass);
        }
    }
}
