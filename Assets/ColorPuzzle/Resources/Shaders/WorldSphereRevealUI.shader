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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

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
                float rawDepth = SampleSceneDepth(input.uv);
                #if UNITY_REVERSED_Z
                if (rawDepth <= 0.00001) return half4(0, 0, 0, 0);
                #else
                if (rawDepth >= 0.99999) return half4(0, 0, 0, 0);
                #endif

                float3 worldPosition = ComputeWorldSpacePosition(
                    input.uv, rawDepth, _RevealInverseViewProjection);
                float distanceFromPillar = distance(worldPosition, _RevealOriginRadius.xyz);
                float mask = 1.0 - smoothstep(
                    _RevealOriginRadius.w - _RevealEdgeFeather,
                    _RevealOriginRadius.w + _RevealEdgeFeather,
                    distanceFromPillar);

                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                return half4(color.rgb, color.a * mask);
            }
            ENDHLSL
        }
    }
}
