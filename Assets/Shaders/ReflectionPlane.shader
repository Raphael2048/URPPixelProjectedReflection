Shader "ReflectionPlane"
{
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True"}
        LOD 300

        Pass
        {
            Tags { "RenderType"="Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True"}
//            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM

            #pragma shader_feature _FULLSCREEN
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Function.hlsl"
            
            
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
            Texture2D<float4> _CameraOpaqueTexture;
            SAMPLER(sampler_CameraOpaqueTexture);
            float4 _TextureSize;
            float4 _ReflectionPlane;
            float4x4 _ClipToWorldMatrix;

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;

            // 此时从全屏pass渲染而来，从裁剪空间坐标，反推应该对应的世界空间坐标，由此可得到无限大平面上的反射方向
            #if defined _FULLSCREEN
                float4 clipPos = vertexInput.positionCS / vertexInput.positionCS.w;
                float4 PosWS = mul(_ClipToWorldMatrix, float4(clipPos.xy, 0.5, 1));
                PosWS = PosWS / PosWS.w;
                output.viewDirWS = GetCameraPositionWS() - PosWS.xyz;
            #else
                output.viewDirWS = GetCameraPositionWS() - vertexInput.positionWS;
            #endif
                
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                int2 coord = input.positionCS.xy;
                uint EncodeValue = _IntermediateTexture.Load(int3(coord, 0));
                if (EncodeValue < PROJECTION_CLEAR_VALUE)
                {
                    int2 offset = DecodeProjectionBufferValue(EncodeValue);
                    int2 ReflectedCoord = coord - offset;
                    float2 uv = (ReflectedCoord + 0.5f) * _TextureSize.zw;
                    return SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, uv);
                }
                else
                {
                    float3 viewDirWS = normalize(input.viewDirWS);
                    // return float4(viewDirWS, 1);
                    float3 reflectDir = reflect(-viewDirWS, _ReflectionPlane.xyz);
                    half4 encodedIrradiance = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, reflectDir, 0);
                    #if !defined(UNITY_USE_NATIVE_HDR)
                        half3 irradiance = DecodeHDREnvironment(encodedIrradiance, unity_SpecCube0_HDR);
                    #else
                        half3 irradiance = encodedIrradiance.rgb;
                    #endif
                    return float4(irradiance, 1);
                }
                
            }


            #pragma vertex vert
            #pragma fragment frag

            
            ENDHLSL
        }
    }
}
