Shader "Hidden/PPR/Filter"
{
    
    Properties
    {
        [HideInInspector] _MainTex("Base (RGB)", 2D) = "white" {}
    }
    HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        #pragma shader_feature_local _USE_INTENSITY_TEXTURE_
    
        TEXTURE2D(_MainTex);
        float4 _MainTex_TexelSize;
        TEXTURE2D(_CopyTexture);
        TEXTURE2D(_IntensityHalf);
        TEXTURE2D(_IntensityFull);
        TEXTURE2D(_MirrorOriginTexture);
    
        SAMPLER(sampler_LinearClamp);
    
        float _BlurSize;
        // float _PreMultipy;
    
        const static int kTapCount = 5;
        const static float kOffsets[] = {
            -3.23076923,
            -1.38461538,
             0.00000000,
             1.38461538,
             3.23076923
        };
        const static half kCoeffs[] = {
             0.07027027,
             0.31621622,
             0.22702703,
             0.31621622,
             0.07027027
        };

        struct Attributes
        {
            float4 positionOS       : POSITION;
            float2 uv               : TEXCOORD0;
        };

        struct Varyings
        {
            float2 uv        : TEXCOORD0;
            float4 vertex : SV_POSITION;
        };

        Varyings vert(Attributes input)
        {
            Varyings output = (Varyings)0;

            VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
            output.vertex = vertexInput.positionCS;
            output.uv = input.uv;

            return output;
        }
    
        half4 blur_h(Varyings input) : SV_Target
        {
            float texelSize = _MainTex_TexelSize.x * _BlurSize;
            half3 color = 0;
            UNITY_UNROLL
            for (int i = 0; i < kTapCount; i++)
            {
                color += SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, input.uv + float2(texelSize * kOffsets[i], 0)).rgb * kCoeffs[i];
            }
            return half4(color, 1);
        }

        half4 blur_v(Varyings input) : SV_Target
        {
            float texelSize = _MainTex_TexelSize.y * _BlurSize;
            half3 color = 0;
            UNITY_UNROLL
            for (int i = 0; i < kTapCount; i++)
            {
                color += SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, input.uv + float2(0, texelSize * kOffsets[i])).rgb * kCoeffs[i];
            }
            return half4(color, 1);
        }

        half4 blur_h_adaptive(Varyings input) : SV_Target
        {
            half BaseIntensity = SAMPLE_TEXTURE2D_X(_IntensityHalf, sampler_LinearClamp, input.uv);
            float texelSize = _MainTex_TexelSize.x * _BlurSize * BaseIntensity;
            
            float4 acc = 0;
            UNITY_UNROLL
            for (int i = 0; i < kTapCount; i++)
            {
                float2 SampleCoord = input.uv + float2(texelSize * kOffsets[i], 0);
                half sampleIntensity = SAMPLE_TEXTURE2D_X(_IntensityHalf, sampler_LinearClamp, SampleCoord);
                half3 sampleColor = SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, SampleCoord);

                float weight = saturate(1.0 - (sampleIntensity - BaseIntensity));
                acc += half4(sampleColor, 1) * kCoeffs[i] * weight;
            }
            acc.xyz /= acc.w + 1e-4;
            return half4(acc.xyz, 1.0);
        }

        float4 blur_v_adaptive(Varyings input) : SV_Target
        {
            half BaseIntensity = SAMPLE_TEXTURE2D_X(_IntensityHalf, sampler_LinearClamp, input.uv);
            float texelSize = _MainTex_TexelSize.y * _BlurSize * BaseIntensity;
            
            float4 acc = 0;
            UNITY_UNROLL
            for (int i = 0; i < kTapCount; i++)
            {
                float2 SampleCoord = input.uv + float2(0, texelSize * kOffsets[i]);
                half sampleIntensity = SAMPLE_TEXTURE2D_X(_IntensityHalf, sampler_LinearClamp, SampleCoord);
                half3 sampleColor = SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, SampleCoord);

                float weight = saturate(1.0 - (sampleIntensity - BaseIntensity));
                acc += half4(sampleColor, 1) * kCoeffs[i] * weight;
            }
            acc.xyz /= acc.w + 1e-4;
            return half4(acc.xyz, 1.0);
        }

        half4 composite(Varyings input) : SV_Target
        {
            half3 color = SAMPLE_TEXTURE2D_X(_MirrorOriginTexture, sampler_LinearClamp, input.uv);
            half intensity = SAMPLE_TEXTURE2D_X(_IntensityFull, sampler_LinearClamp, input.uv);
            half3 blurColor = SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, input.uv);
            
            half3 dstColor = 0.0;
            half dstAlpha = 1.0f;
            
            UNITY_BRANCH
            if (intensity > 0)
            {
                half blend = sqrt(intensity * TWO_PI);
                dstColor = blurColor * saturate(blend);
                dstAlpha = saturate(1.0f - blend);
            }
            
            return half4(color * dstAlpha + dstColor, 1.0f); 
        }

        struct ReflectionOutput
        {
            half3 color : SV_Target0;
            half  blur   : SV_Target1;
        };

        ReflectionOutput prefilter(Varyings input)
        {
            ReflectionOutput output;
            output.blur = SAMPLE_TEXTURE2D_X(_IntensityFull, sampler_LinearClamp, input.uv);
            output.color = SAMPLE_TEXTURE2D_X(_MirrorOriginTexture, sampler_LinearClamp, input.uv);
            // output.color = SAMPLE_TEXTURE2D_X(_MirrorOriginTexture, sampler_LinearClamp, input.uv) * output.blur;
            return output;
        }
            
        
        
    ENDHLSL
    
    
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"}
        ZTest Always Cull Off ZWrite Off
        LOD 100
        
        Pass
        {
            Name "Blur H"
            HLSLPROGRAM
                #pragma vertex vert
                #pragma fragment blur_h
            ENDHLSL
        }
        
        Pass
        {
            Name "Blur V"
            HLSLPROGRAM
                #pragma vertex vert
                #pragma fragment blur_v
            ENDHLSL
        }
        
        Pass
        {
            Name "Blur H Adaptive"
            HLSLPROGRAM
                #pragma vertex vert
                #pragma fragment blur_h_adaptive
            ENDHLSL
        }
        
        Pass
        {
            Name "Blur V Adaptive"
            HLSLPROGRAM
                #pragma vertex vert
                #pragma fragment blur_v_adaptive
            ENDHLSL
        }

        
        Pass
        {
            Name "Composite"
            HLSLPROGRAM
                #pragma vertex vert
                #pragma fragment composite
            ENDHLSL
        }
        
        Pass
        {
            Name "PreFilter"
            HLSLPROGRAM
                #pragma vertex vert
                #pragma fragment prefilter
            ENDHLSL
        }
    }
}
