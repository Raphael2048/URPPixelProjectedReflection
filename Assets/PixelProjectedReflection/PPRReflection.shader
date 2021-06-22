Shader "Hidden/PPR/ReflectionPlane"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True"}
        LOD 300
        
        HLSLINCLUDE
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
            real4 _ReflectionProbe_HDR;
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

            void LoadInput(Varyings input, out half3 color, out float intensity)
            {
                int2 coord = input.positionCS.xy;
                color = half3(0, 0, 0);
                half alpha = 0;
                uint EncodeValue = _IntermediateTexture.Load(int3(coord, 0));

                UNITY_BRANCH
                if (EncodeValue < PROJECTION_CLEAR_VALUE)
                {
                    int2 offset;
                    int distance;
                    DecodeProjectionBufferValue(EncodeValue, offset, distance);
                    intensity = distance * 0.015625;
                    int2 ReflectedCoord = coord - offset;
                    half2 uv = (ReflectedCoord + 0.5f) * _TextureSize.zw;
                    color = SAMPLE_TEXTURE2D(_CameraColorTexture, sampler_CameraColorTexture, uv);
                    half2 vignette = saturate(abs(uv * 2.0 - 1.0) * 5.0 - 4.0);
                    alpha = saturate(1 - dot(vignette, vignette));
                }
                else
                {
                    intensity = 1;
                }

                UNITY_BRANCH
                if(alpha < 1)
                {
                    float3 viewDirWS = normalize(input.viewDirWS);
                    float3 reflectDir = reflect(-viewDirWS, _ReflectionPlane.xyz);
                    half4 encodedIrradiance = SAMPLE_TEXTURECUBE_LOD(_ReflectionProbe, sampler_ReflectionProbe, reflectDir, 0);
                    #if !defined(UNITY_USE_NATIVE_HDR)
                        half3 irradiance = DecodeHDREnvironment(encodedIrradiance, _ReflectionProbe_HDR);
                    #else
                        half3 irradiance = encodedIrradiance.rgb;
                    #endif
                    // half3 irradiance = encodedIrradiance.rgb;

                    color = lerp(irradiance, color, alpha);
                }
            }

            half4 frag(Varyings input) : SV_Target
            {
                half3 color;
                float intensity;
                LoadInput(input, color, intensity);
                return float4(color, 1);
            }

            struct ReflectionOutput
            {
                half3 color : SV_Target0;
                half  blur   : SV_Target1;
            };

            ReflectionOutput frag_mrt(Varyings input)
            {
                half3 color;
                float intensity;
                LoadInput(input, color, intensity);
                ReflectionOutput output;
                output.color = color;
                output.blur = intensity;
                return output;
            }
        ENDHLSL

        Pass
        {
            Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True"}
            ZTEST ALWAYS ZWRITE OFF CULL OFF
            
            HLSLPROGRAM
                #pragma vertex vert
                #pragma fragment frag
            ENDHLSL
        }
        
        Pass
        {
            Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True"}
            ZTEST ALWAYS ZWRITE OFF CULL OFF
            
            HLSLPROGRAM
                #pragma vertex vert
                #pragma fragment frag_mrt
            ENDHLSL
        }
    }
}
