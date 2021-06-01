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
        private RenderTargetHandle _tempRT1, _tempRT2, _tempRT3, _tempRT4, _tempRT5, _final, _intensityFull, _intensityHalf;
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
            
            _tempRT1.Init("_MirrorOriginTexture");
            _tempRT2.Init("PPR_TEMP2");
            _tempRT3.Init("PPR_TEMP3");
            _tempRT4.Init("PPR_TEMP4");
            _tempRT5.Init("PPR_TEMP5");
            _final.Init("_MirrorTexture");
            _intensityFull.Init("_IntensityFull");
            _intensityHalf.Init("_IntensityHalf");
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDesc)
        {
            if (_plane == null)
            {
                _plane = Object.FindObjectOfType<PixelProjectedReflectionPlane>();
            }

            if (_plane)
            {
                if (_plane.PreFilterValue > 0 && !_plane.AdaptiveBlur)
                {
                    cmd.GetTemporaryRT(_final.id, cameraTextureDesc.width / 2, cameraTextureDesc.height / 2, 0, FilterMode.Bilinear, cameraTextureDesc.graphicsFormat);
                }
                else
                {
                    cmd.GetTemporaryRT(_final.id, cameraTextureDesc.width , cameraTextureDesc.height, 0, FilterMode.Bilinear, cameraTextureDesc.graphicsFormat);
                }
                ConfigureTarget(_final.Identifier());
            }

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
                    cmd.SetComputeFloatParam(PPRProjection, "_BlurMaxDistance", 1.0f / _plane.BlurMaxDistance);
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

                        if (_plane.AdaptiveBlur)
                        {
                            cmd.GetTemporaryRT(_intensityFull.id, cameraTextureDesc.width, cameraTextureDesc.height,0, FilterMode.Bilinear, GraphicsFormat.R8_UNorm);
                            cmd.GetTemporaryRT(_intensityHalf.id, cameraTextureDesc.width / 2, cameraTextureDesc.height / 2, 0, FilterMode.Bilinear, GraphicsFormat.R8_UNorm);
                            cmd.GetTemporaryRT(_tempRT3.id, cameraTextureDesc.width / 2 , cameraTextureDesc.height / 2, 0, FilterMode.Bilinear, cameraTextureDesc.graphicsFormat);
                            
                            var rts = new RenderTargetIdentifier[] {_tempRT1.Identifier(), _intensityFull.Identifier()};
                            cmd.SetRenderTarget(rts, _tempRT1.Identifier());
                            cmd.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
                            cmd.SetViewport(new Rect(0, 0, cameraTextureDesc.width, cameraTextureDesc.height));
                            cmd.DrawMesh(RenderingUtils.fullscreenMesh, Matrix4x4.identity, PPRReflection, 0, 1);

                            // rts = new RenderTargetIdentifier[] {_tempRT3.Identifier(), _intensityHalf.Identifier()};
                            // cmd.SetRenderTarget(rts, _tempRT3.Identifier());
                            // cmd.DrawMesh(RenderingUtils.fullscreenMesh, Matrix4x4.identity, PPRFilter, 0, 5);
                            cmd.SetViewProjectionMatrices(camera.worldToCameraMatrix, camera.projectionMatrix);
                            cmd.Blit(_tempRT1.Identifier(), _tempRT3.Identifier());
                            cmd.Blit(_intensityFull.Identifier(), _intensityHalf.Identifier());
                            
                            
                            PPRFilter.SetFloat("_BlurSize", _plane.PreFilterValue);
                            // cmd.SetGlobalFloat("_PreMultipy", 1.0f);
                            cmd.Blit(_tempRT3.Identifier(), _tempRT2.Identifier(), PPRFilter, 2);
                            
                            cmd.SetGlobalFloat("_PreMultipy", 0.0f);
                            cmd.Blit(_tempRT2.Identifier(), _tempRT3.Identifier(), PPRFilter, 3);

                            if (_plane.DualBlur)
                            {
                                cmd.GetTemporaryRT(_tempRT4.id, cameraTextureDesc.width / 4,
                                    cameraTextureDesc.height / 4, 0, FilterMode.Bilinear,
                                    cameraTextureDesc.graphicsFormat);
                                cmd.GetTemporaryRT(_tempRT5.id, cameraTextureDesc.width / 4,
                                    cameraTextureDesc.height / 4, 0, FilterMode.Bilinear,
                                    cameraTextureDesc.graphicsFormat);
                                cmd.Blit(_tempRT3.Identifier(), _tempRT4.Identifier());
                                cmd.Blit(_tempRT4.Identifier(), _tempRT5.Identifier(), PPRFilter, 2);
                                cmd.Blit(_tempRT5.Identifier(), _tempRT4.Identifier(), PPRFilter, 3);
                                cmd.Blit(_tempRT4.Identifier(), _final.Identifier(), PPRFilter, 4);
                            }
                            else
                            {
                                cmd.Blit(_tempRT3.Identifier(), _final.Identifier(), PPRFilter, 4);
                            }
                        }
                        else
                        {
                            cmd.Blit(-1, _tempRT1.Identifier(), PPRReflection, 0, 0);
                            PPRFilter.SetFloat("_BlurSize", _plane.PreFilterValue);
                            // Downsample
                            cmd.Blit(_tempRT1.Identifier(), _final.Identifier());
                            cmd.Blit(_final.Identifier(), _tempRT2.Identifier(), PPRFilter, 0);
                            cmd.Blit(_tempRT2.Identifier(), _final.Identifier(), PPRFilter, 1);
                            if (_plane.DualBlur)
                            {
                                cmd.Blit(_final.Identifier(), _tempRT2.Identifier(), PPRFilter, 0);
                                cmd.Blit(_tempRT2.Identifier(), _final.Identifier(), PPRFilter, 1);
                            }
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
            cmd.ReleaseTemporaryRT(_tempRT3.id);
            cmd.ReleaseTemporaryRT(_tempRT4.id);
            cmd.ReleaseTemporaryRT(_tempRT5.id);
            cmd.ReleaseTemporaryRT(_final.id);
            cmd.ReleaseTemporaryRT(_intensityFull.id);
            cmd.ReleaseTemporaryRT(_intensityHalf.id);
        }
    }
    
}