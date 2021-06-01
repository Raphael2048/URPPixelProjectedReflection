using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UnityEngine.Experiemntal.Rendering.Universal
{
    public class PixelProjectedReflectionRendererFeature : ScriptableRendererFeature
    {
        [Serializable]
        public class Settings
        {
            public ComputeShader projection;
            public Shader reflection;
            public Shader filter;
        }

        public Settings _settings;
        
        private DrawReflectionPass _pass;
        public override void Create()
        {
            _pass = new DrawReflectionPass(_settings);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(_pass);
        }
        
    }
}