Shader "Hidden/PPR/ReflectionPlane"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True"}
        LOD 300

        Pass
        {
            Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True"}
            ZTEST ALWAYS ZWRITE OFF CULL OFF
            HLSLPROGRAM

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "PPRFunction.hlsl"
            
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 viewDirWS : TEXCOORD0;
            };
            Texture2D<uint> _IntermediateTexture;
            Texture2D<float4> _CameraColorTexture;
            SAMPLER(sampler_CameraColorTexture);

            
            
            TextureCube<float4> _ReflectionProbe;
            SAMPLER(sampler_ReflectionProbe);
            float4 _TextureSize;
            float4 _ReflectionPlane;
            float4x4 _ClipToWorldMatrix;

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;

                float4 clipPos = vertexInput.positionCS / vertexInput.positionCS.w;
                float4 PosWS = mul(_ClipToWorldMatrix, float4(clipPos.xy, 0.5, 1));
                PosWS = PosWS / PosWS.w;
                output.viewDirWS = GetCameraPositionWS() - PosWS.xyz;
                
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                int2 coord = input.positionCS.xy;
                half3 color = half3(0, 0, 0);
                half alpha = 0;
                uint EncodeValue = _IntermediateTexture.Load(int3(coord, 0));
                if (EncodeValue < PROJECTION_CLEAR_VALUE)
                {
                    int2 offset = DecodeProjectionBufferValue(EncodeValue);
                    int2 ReflectedCoord = coord - offset;
                    half2 uv = (ReflectedCoord + 0.5f) * _TextureSize.zw;
                    color = SAMPLE_TEXTURE2D(_CameraColorTexture, sampler_CameraColorTexture, uv);
                    half2 vignette = saturate(abs(uv * 2.0f - 1.0f) * 5.0f - 4.0f);
                    alpha = saturate(1 - dot(vignette, vignette));
                }

                if(alpha < 1)
                {
                    float3 viewDirWS = normalize(input.viewDirWS);
                    float3 reflectDir = reflect(-viewDirWS, _ReflectionPlane.xyz);
                    half4 encodedIrradiance = SAMPLE_TEXTURECUBE_LOD(_ReflectionProbe, sampler_ReflectionProbe, reflectDir, 0);
                    half3 irradiance = encodedIrradiance.rgb;

                    color = lerp(irradiance, color, alpha);
                }
                
                return float4(color, 1);
                
            }


            #pragma vertex vert
            #pragma fragment frag

            
            ENDHLSL
        }
    }
}
