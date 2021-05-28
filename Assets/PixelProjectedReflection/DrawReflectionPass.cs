using System.Collections.Generic;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

namespace UnityEngine.Experiemntal.Rendering.Universal
{
    internal class DrawReflectionPass : ScriptableRenderPass
    {
        string m_ProfilerTag;
        ProfilingSampler m_ProfilingSampler;
        private ComputeShader PPRProjection;
        private Shader PPRReflectionPS, PPRFilterPS;
        private Material PPRReflection, PPRFilter;
        private RenderTexture intermediateTexture;
        private RenderTargetHandle _tempRT1, _tempRT2;
        private RenderTargetHandle _final;
        private PixelProjectedReflectionPlane _plane;

        public DrawReflectionPass(PixelProjectedReflectionRendererFeature.Settings settings)
        { 
            m_ProfilerTag = "PixelProjectedReflection"; 
            m_ProfilingSampler = new ProfilingSampler(m_ProfilerTag);
            renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;

            PPRProjection = settings.projection;
            PPRReflectionPS = settings.reflection;
            PPRFilterPS = settings.filter;
            PPRReflection = new Material(settings.reflection);
            PPRFilter = new Material(settings.filter);
            
            _tempRT1.Init("PPR_TEMP1");
            _tempRT2.Init("PPR_TEMP2");
            _final.Init("_MirrorTexture");
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDesc)
        {
            if (_plane == null)
            {
                _plane = Object.FindObjectOfType<PixelProjectedReflectionPlane>();
            }

            if (_plane.PreFilterValue > 0)
            {
                cmd.GetTemporaryRT(_final.id, cameraTextureDesc.width / 2, cameraTextureDesc.height / 2, 0, FilterMode.Bilinear, cameraTextureDesc.graphicsFormat);
            }
            else
            {
                cmd.GetTemporaryRT(_final.id, cameraTextureDesc.width , cameraTextureDesc.height, 0, FilterMode.Bilinear, cameraTextureDesc.graphicsFormat);
            }
            ConfigureTarget(_final.Identifier());
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (!_plane || !_plane.enabled) return;
            
            var renderer = _plane.GetComponent<Renderer>();
            if (!renderer.isVisible) return;

            if (PPRReflection == null) PPRReflection = new Material(PPRReflectionPS);
            if (PPRFilter == null) PPRFilter = new Material(PPRFilterPS);
            

            CommandBuffer cmd = CommandBufferPool.Get(m_ProfilerTag);
            using (new ProfilingScope(cmd, m_ProfilingSampler))
            {
                Camera camera = renderingData.cameraData.camera;
                Matrix4x4 m = _plane.gameObject.transform.localToWorldMatrix;
                Plane p = new Plane(Vector3.up, Vector3.zero);
                p = m.TransformPlane(p);
                var cameraTextureDesc = renderingData.cameraData.cameraTargetDescriptor;
                var textureSize = new Vector4(cameraTextureDesc.width, cameraTextureDesc.height,
                    1.0f / cameraTextureDesc.width, 1.0f / cameraTextureDesc.height);
                if (intermediateTexture == null || intermediateTexture.width != cameraTextureDesc.width ||
                    intermediateTexture.height != cameraTextureDesc.height)
                {
                    if(intermediateTexture != null) intermediateTexture.Release();
                    intermediateTexture = new RenderTexture(cameraTextureDesc.width, cameraTextureDesc.height, 0, GraphicsFormat.R32_UInt);
                    intermediateTexture.enableRandomWrite = true;
                    intermediateTexture.Create();
                }

               
                // if (debugTexture == null)
                // {
                //     debugTexture = new RenderTexture(cameraTextureDesc.width, cameraTextureDesc.height, 0, GraphicsFormat.R16G16_SFloat);
                //     debugTexture.enableRandomWrite = true;
                //     debugTexture.Create();
                // }
                var reflectionPlane = new Vector4(p.normal.x, p.normal.y, p.normal.z, p.distance);
                {
                    var WorldToClip = GL.GetGPUProjectionMatrix(camera.projectionMatrix, false) * camera.worldToCameraMatrix;
                    cmd.SetComputeMatrixParam(PPRProjection, "_WorldToClipMatrix", WorldToClip);
                    cmd.SetComputeMatrixParam(PPRProjection, "_ClipToWorldMatrix", WorldToClip.inverse);
                    cmd.SetComputeVectorParam(PPRProjection, "_TextureSize", textureSize);
                    cmd.SetComputeVectorParam(PPRProjection, "_WSCameraPos", camera.transform.position);
                    cmd.SetComputeVectorParam(PPRProjection, "_ReflectionPlane", reflectionPlane);
                    cmd.SetComputeTextureParam(PPRProjection, 0, "_IntermediateTexture", intermediateTexture);
                    cmd.DispatchCompute(PPRProjection, 0, (cameraTextureDesc.width + 7) / 8, (cameraTextureDesc.height + 7) / 8, 1);

                    cmd.SetComputeTextureParam(PPRProjection, 1, "_IntermediateTexture", intermediateTexture);
                    // cmd.SetComputeTextureParam(PPRProjection, 1, "_DebugTexture", debugTexture);

                    cmd.DispatchCompute(PPRProjection, 1, (cameraTextureDesc.width + 7) / 8, (cameraTextureDesc.height + 7) / 8, 1);
                }

                {
                    var matrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true) * camera.worldToCameraMatrix;
                    PPRReflection.SetTexture("_IntermediateTexture", intermediateTexture);
                    PPRReflection.SetVector("_TextureSize", textureSize);
                    PPRReflection.SetVector("_ReflectionPlane", reflectionPlane);
                    PPRReflection.SetMatrix("_ClipToWorldMatrix", matrix.inverse);
                    List<ReflectionProbeBlendInfo> probes = new List<ReflectionProbeBlendInfo>();
                    renderer.GetClosestReflectionProbes(probes);
                    if (probes.Count > 0)
                    {
                        PPRReflection.SetTexture("_ReflectionProbe", probes[0].probe.texture);
                    }

                    if (_plane.PreFilterValue > 0)
                    {
                        cmd.GetTemporaryRT(_tempRT1.id, cameraTextureDesc.width , cameraTextureDesc.height, 0, FilterMode.Bilinear, cameraTextureDesc.graphicsFormat);
                        cmd.GetTemporaryRT(_tempRT2.id, cameraTextureDesc.width / 2 , cameraTextureDesc.height / 2, 0, FilterMode.Bilinear, cameraTextureDesc.graphicsFormat);
                        
                        cmd.Blit(-1,_tempRT1.Identifier(), PPRReflection);
                        PPRFilter.SetFloat("_BlurSize", _plane.PreFilterValue);
                        cmd.Blit(_tempRT1.Identifier(), _tempRT2.Identifier(), PPRFilter, 0);
                        cmd.Blit(_tempRT2.Identifier(), _final.Identifier(), PPRFilter, 1);
                        if (_plane.DualBlur)
                        {
                            cmd.Blit(_final.Identifier(), _tempRT2.Identifier(), PPRFilter, 0);
                            cmd.Blit(_tempRT2.Identifier(), _final.Identifier(), PPRFilter, 1);
                        }
                    }
                    else
                    {
                        
                        cmd.Blit(-1,_final.Identifier(), PPRReflection);
                    }
                }
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        
        public override void FrameCleanup(CommandBuffer cmd)
        {
            cmd.ReleaseTemporaryRT(_tempRT1.id);
            cmd.ReleaseTemporaryRT(_tempRT2.id);
            cmd.ReleaseTemporaryRT(_final.id);
        }
    }
    
}