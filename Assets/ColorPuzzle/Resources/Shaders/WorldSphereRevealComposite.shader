Shader "ColorPuzzle/WorldSphereRevealComposite"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "WorldSphereRevealComposite"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes { uint vertexID : SV_VertexID; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_ColorPuzzleRevealTexture);
            SAMPLER(sampler_ColorPuzzleRevealTexture);
            float4 _RevealOriginRadius;
            float _RevealEdgeFeather;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                output.uv = GetFullScreenTriangleTexCoord(input.vertexID);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float rawDepth = SampleSceneDepth(input.uv);
                #if UNITY_REVERSED_Z
                if (rawDepth <= 0.00001) return half4(0, 0, 0, 0);
                #else
                if (rawDepth >= 0.99999) return half4(0, 0, 0, 0);
                rawDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
                #endif

                // The inverse view-projection matrix belongs to the camera currently
                // running this render pass, not the colour texture camera. That makes
                // the edge a sphere fixed around the heart while the player looks around.
                float3 worldPosition = ComputeWorldSpacePosition(
                    input.uv, rawDepth, UNITY_MATRIX_I_VP);
                float worldDistance = distance(worldPosition, _RevealOriginRadius.xyz);
                float mask = 1.0 - smoothstep(
                    _RevealOriginRadius.w - _RevealEdgeFeather,
                    _RevealOriginRadius.w + _RevealEdgeFeather,
                    worldDistance);
                half4 color = SAMPLE_TEXTURE2D(
                    _ColorPuzzleRevealTexture,
                    sampler_ColorPuzzleRevealTexture,
                    input.uv);
                return half4(color.rgb, color.a * mask);
            }
            ENDHLSL
        }
    }
}
