Shader "Hidden/PPR/Filter"
{
    
    Properties
    {
        [HideInInspector] _MainTex("Base (RGB)", 2D) = "white" {}
    }
    HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        float4 _MainTex_TexelSize;

        TEXTURE2D(_CopyTexture);
        SAMPLER(sampler_CopyTexture);

        float _BlurSize;

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
        float4 blur_h(Varyings input) : SV_Target
        {
            float texelSize = _MainTex_TexelSize.x * _BlurSize;
            half3 c0 = SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, input.uv - float2(texelSize * 3.23076923, 0.0)).rgb;
            half3 c1 = SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, input.uv - float2(texelSize * 1.38461538, 0.0)).rgb;
            half3 c2 = SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, input.uv).rgb;  
            half3 c3 = SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, input.uv + float2(texelSize * 1.38461538, 0.0)).rgb;
            half3 c4 = SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, input.uv + float2(texelSize * 3.23076923, 0.0)).rgb;

            half3 color = c0 * 0.07027027 + c1 * 0.31621622
                    + c2 * 0.22702703
                    + c3 * 0.31621622 + c4 * 0.07027027;
            return float4(color, 1);
        }

        float4 blur_v(Varyings input) : SV_Target
        {
            float texelSize = _MainTex_TexelSize.y * _BlurSize;
            half3 c0 = SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, input.uv - float2(0.0, texelSize * 3.23076923)).rgb;
            half3 c1 = SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, input.uv - float2(0.0, texelSize * 1.38461538)).rgb;
            half3 c2 = SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, input.uv).rgb;  
            half3 c3 = SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, input.uv + float2(0.0, texelSize * 1.38461538)).rgb;
            half3 c4 = SAMPLE_TEXTURE2D_X(_MainTex, sampler_MainTex, input.uv + float2(0.0, texelSize * 3.23076923)).rgb;

            half3 color = c0 * 0.07027027 + c1 * 0.31621622
                    + c2 * 0.22702703
                    + c3 * 0.31621622 + c4 * 0.07027027;

            return float4(color, 1);
        }
    
        float4 combine(Varyings input) : SV_Target
        {
            float2 uv = input.vertex.xy * (_ScreenParams.zw - 1.0f);
            return SAMPLE_TEXTURE2D_X(_CopyTexture, sampler_CopyTexture, uv);
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
            ZTest LEqual Cull Back ZWrite On 
            Name "Combine"
            HLSLPROGRAM
                #pragma vertex vert
                #pragma fragment combine
            ENDHLSL
        }
    }
}
