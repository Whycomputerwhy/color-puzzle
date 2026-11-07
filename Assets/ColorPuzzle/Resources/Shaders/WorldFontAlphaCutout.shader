Shader "ColorPuzzle/World Font Alpha Cutout"
{
    Properties
    {
        _MainTex ("Baked Font Atlas", 2D) = "white" {}
        _Cutoff ("Edge Cutoff", Range(0.001, 0.5)) = 0.04
    }

    SubShader
    {
        // An alpha-test pass is still normal world geometry: it writes depth for
        // ink pixels, but discards the blank area around each glyph.
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest" "RenderType" = "TransparentCutout" }

        Pass
        {
            Name "BakedFontCutout"
            Cull Off
            ZWrite On
            ZTest LEqual
            AlphaToMask On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // ===== ART-DIRECTABLE PARAMETERS =====
            // _Cutoff trades anti-aliased softness for a tighter ink silhouette.
            CBUFFER_START(UnityPerMaterial)
                float _Cutoff;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a * input.color.a;
                clip(alpha - _Cutoff);
                return half4(input.color.rgb, 1.0h);
            }
            ENDHLSL
        }
    }
}
