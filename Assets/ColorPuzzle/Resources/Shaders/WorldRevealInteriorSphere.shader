Shader "ColorPuzzle/WorldRevealInteriorSphere"
{
    Properties
    {
        _Color("Blue Volume Tint", Color) = (0.10, 0.52, 0.95, 0.16)
        _FresnelStrength("Edge Strength", Range(0, 1)) = 0.35
        _FresnelPower("Edge Tightness", Range(0.5, 8)) = 3
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest LEqual
        // Render only the sphere's inside faces: the effect is a volume the
        // player enters, rather than an opaque ball in front of the scene.
        Cull Front

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _FresnelStrength;
                float _FresnelPower;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 viewDirection = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                half edge = pow(1.0h - saturate(abs(dot(viewDirection, normalize(input.normalWS)))), _FresnelPower);
                half alpha = saturate(_Color.a + edge * _FresnelStrength);
                return half4(_Color.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
