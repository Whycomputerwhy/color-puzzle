Shader "ColorPuzzle/Underwater Gradient Backdrop"
{
    Properties
    {
        _UpperColor("Upper Water", Color) = (0.10, 0.42, 0.56, 1)
        _LowerColor("Lower Water", Color) = (0.025, 0.12, 0.22, 1)
        _VerticalBlendPower("Vertical Blend Power", Range(0.25, 4)) = 1.15
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Background" "RenderType" = "Opaque" }
        Pass
        {
            Name "OceanGradient"
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _UpperColor;
                float4 _LowerColor;
                float _VerticalBlendPower;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float verticalBlend = pow(saturate(input.uv.y), _VerticalBlendPower);
                return lerp(_LowerColor, _UpperColor, verticalBlend);
            }
            ENDHLSL
        }
    }
}
