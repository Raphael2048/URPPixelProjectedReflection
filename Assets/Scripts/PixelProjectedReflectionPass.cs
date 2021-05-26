using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UnityEngine.Experimental.Rendering.Universal
{
    internal class PixelProjectedReflectionPass : ScriptableRenderPass
    {

        private ComputeShader _cs;

        private RenderTexture intermediateTexture;
        private RenderTargetHandle _tempRT, _tempRT2;

        private Material _reflection;
        private Material _fullScreenReflection, _filter;

        private RenderTargetIdentifier _color, _depth;

        public PixelProjectedReflectionPass(PixelProjectedReflectionFeature.Settings settings)
        { 
            _cs = settings.projection;
            _tempRT.Init("TEMP0");
            _tempRT2.Init("TEMP1");
            this.renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
            var refShader = settings.reflection;
            _reflection = new Material(refShader); 
            _fullScreenReflection = new Material(refShader);
            _fullScreenReflection.EnableKeyword("_FULLSCREEN");

            _filter = new Material(settings.filter);
        }

        public void Setup(RenderTargetIdentifier color, RenderTargetIdentifier depth)
        {
            this._color = color;
            this._depth = depth;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            PixelProjectedReflectionPlane plane = Object.FindObjectOfType<PixelProjectedReflectionPlane>();
            if (plane)
            {
                Matrix4x4 m = plane.gameObject.transform.localToWorldMatrix;
                Plane p = new Plane(Vector3.up, Vector3.zero);
                p = m.TransformPlane(p);
                CommandBuffer cmd = CommandBufferPool.Get("PixelProjectedReflection");
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
                
                var camera = renderingData.cameraData.camera;
                var WorldToClip = GL.GetGPUProjectionMatrix(camera.projectionMatrix, false) * camera.worldToCameraMatrix;
                var reflectionPlane = new Vector4(p.normal.x, p.normal.y, p.normal.z, p.distance);
                cmd.SetComputeMatrixParam(_cs, "_WorldToClipMatrix", WorldToClip);
                cmd.SetComputeMatrixParam(_cs, "_ClipToWorldMatrix", WorldToClip.inverse);
                cmd.SetComputeVectorParam(_cs, "_TextureSize", textureSize);
                cmd.SetComputeVectorParam(_cs, "_WSCameraPos", camera.transform.position);
                cmd.SetComputeVectorParam(_cs, "_ReflectionPlane", reflectionPlane);
                
                cmd.SetComputeTextureParam(_cs, 0, "_IntermediateTexture", intermediateTexture);
                cmd.DispatchCompute(_cs, 0, (cameraTextureDesc.width + 7) / 8, (cameraTextureDesc.height + 7) / 8, 1);
                
                // _cs.SetTextureFromGlobal(1, "_CameraDepthTexture", "_CameraDepthTexture");
                // cmd.SetComputeTextureParam(_cs, 1, "_Depth", new RenderTargetIdentifier(renderingData.cameraData.targetTexture.depth));
                cmd.SetComputeTextureParam(_cs, 1, "_IntermediateTexture", intermediateTexture);
                cmd.DispatchCompute(_cs, 1, (cameraTextureDesc.width + 7) / 8, (cameraTextureDesc.height + 7) / 8, 1);

                // if (plane.PreFilterValue > 0)
                {
                    var matrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true) * camera.worldToCameraMatrix;
                    _fullScreenReflection.SetTexture("_IntermediateTexture", intermediateTexture);
                    _fullScreenReflection.SetVector("_TextureSize", textureSize);
                    _fullScreenReflection.SetVector("_ReflectionPlane", reflectionPlane);
                    _fullScreenReflection.SetMatrix("_ClipToWorldMatrix", matrix.inverse);
                    
                    cmd.GetTemporaryRT(_tempRT.id, cameraTextureDesc.width, cameraTextureDesc.height
                        , 0, FilterMode.Bilinear, cameraTextureDesc.graphicsFormat);
                    
                    cmd.GetTemporaryRT(_tempRT2.id, cameraTextureDesc.width / 2 , cameraTextureDesc.height / 2
                        , 0, FilterMode.Bilinear, cameraTextureDesc.graphicsFormat);
                    
                    cmd.Blit(0,_tempRT.Identifier(), _fullScreenReflection);

                    if (plane.PreFilterValue > 0)
                    {
                        _filter.SetFloat("_BlurSize", plane.PreFilterValue);
                        cmd.Blit(_tempRT.Identifier(), _tempRT2.Identifier(), _filter, 0);
                        cmd.Blit(_tempRT2.Identifier(), _tempRT.Identifier(), _filter, 1);
                        if (plane.DualBlur)
                        {
                            cmd.Blit(_tempRT.Identifier(), _tempRT2.Identifier(), _filter, 0);
                            cmd.Blit(_tempRT2.Identifier(), _tempRT.Identifier(), _filter, 1);
                        }
                    }
                    
                    cmd.SetRenderTarget(_color, _depth);
                    cmd.SetGlobalTexture("_CopyTexture", _tempRT.Identifier());
                    var mesh = plane.gameObject.GetComponent<MeshFilter>();

                    var material = plane.Material;
                    
                    // cmd.DrawMesh(mesh.sharedMesh, plane.transform.localToWorldMatrix, _filter, 0, 2);
                    cmd.DrawMesh(mesh.sharedMesh, plane.transform.localToWorldMatrix, material, 0, 0);
                    
                    
                    cmd.ReleaseTemporaryRT(_tempRT.id);
                    cmd.ReleaseTemporaryRT(_tempRT2.id);
                }
                // else
                // {
                //     // cmd.SetRenderTarget(_color, _depth);
                //     var mesh = plane.gameObject.GetComponent<MeshFilter>();
                //     _reflection.SetTexture("_IntermediateTexture", intermediateTexture);
                //     _reflection.SetVector("_TextureSize", textureSize);
                //     _reflection.SetVector("_ReflectionPlane", reflectionPlane);
                //     cmd.DrawMesh(mesh.sharedMesh, plane.transform.localToWorldMatrix, _reflection, 0);
                //     
                // }
                
                

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
        }

        public override void FrameCleanup(CommandBuffer cmd)
        {
            
        }
    }
}
