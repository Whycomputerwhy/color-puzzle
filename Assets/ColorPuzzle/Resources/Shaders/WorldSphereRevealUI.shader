Shader "ColorPuzzle/UI/WorldSphereReveal"
{
    Properties
    {
        [PerRendererData] _MainTex ("Color Render", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;
            half4 _Color;
            float4 _RevealOriginRadius;
            float _RevealEdgeFeather;
            float4x4 _RevealInverseViewProjection;
            float4 _RevealScreenCenterRadius;
            float _RevealScreenFeather;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.vertex.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color * _Color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float aspect = _ScreenParams.x / max(1.0, _ScreenParams.y);
                float2 fromRevealCenter = input.uv - _RevealScreenCenterRadius.xy;
                fromRevealCenter.x *= aspect;
                float distanceFromRevealCenter = length(fromRevealCenter);
                float mask = 1.0 - smoothstep(
                    _RevealScreenCenterRadius.z - _RevealScreenFeather,
                    _RevealScreenCenterRadius.z + _RevealScreenFeather,
                    distanceFromRevealCenter);

                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                return half4(color.rgb, color.a * mask);
            }
            ENDHLSL
        }
    }
}
