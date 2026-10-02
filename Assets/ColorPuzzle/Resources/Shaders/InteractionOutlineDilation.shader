Shader "Hidden/ColorPuzzle/InteractionOutlineDilation"
{
    Properties
    {
        _Spread("Width (pixels)", Integer) = 18
        _Softness("Softness", Range(0, 1)) = 0.8
        _OutlineColor("Outline Color", Color) = (1, 1, 1, 1)
        _Opacity("Opacity", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        SAMPLER(sampler_BlitTexture);
        int _Spread;
        float _Softness;
        float4 _OutlineColor;
        float _Opacity;
        TEXTURE2D_X(_InteractionObjectMask);
        SAMPLER(sampler_InteractionObjectMask);

        float SoftFalloff(float normalizedDistance)
        {
            float linearFalloff = saturate(1.0 - normalizedDistance);
            float smoothFalloff = linearFalloff * linearFalloff * (3.0 - 2.0 * linearFalloff);
            float exponent = lerp(2.2, 0.7, _Softness);
            return pow(smoothFalloff, exponent);
        }
        ENDHLSL

        Pass
        {
            Name "HorizontalExpansion"
            ZTest Always

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragHorizontal

            float4 FragHorizontal(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                int nearestDistance = _Spread + 1;
                for (int x = -_Spread; x <= _Spread; x++)
                {
                    float2 uv = input.texcoord + float2(_BlitTexture_TexelSize.x * x, 0.0);
                    float occupied = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, uv).a;
                    if (occupied > 0.001)
                        nearestDistance = min(nearestDistance, abs(x));
                }

                float alpha = nearestDistance <= _Spread
                    ? SoftFalloff(nearestDistance / max(1.0, (float)_Spread))
                    : 0.0;
                return float4(1.0, 1.0, 1.0, alpha);
            }
            ENDHLSL
        }

        Pass
        {
            Name "VerticalExpansionAndComposite"
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragVertical

            float4 FragVertical(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float strongest = 0.0;
                for (int y = -_Spread; y <= _Spread; y++)
                {
                    float2 uv = input.texcoord + float2(0.0, _BlitTexture_TexelSize.y * y);
                    float horizontalStrength = SAMPLE_TEXTURE2D_X(
                        _BlitTexture,
                        sampler_BlitTexture,
                        uv).a;
                    float verticalFalloff = SoftFalloff(abs(y) / max(1.0, (float)_Spread));
                    strongest = max(strongest, horizontalStrength * verticalFalloff);
                }

                float objectCoverage = SAMPLE_TEXTURE2D_X(
                    _InteractionObjectMask,
                    sampler_InteractionObjectMask,
                    input.texcoord).a;
                float outlineAlpha = strongest * (1.0 - step(0.001, objectCoverage));
                return float4(_OutlineColor.rgb, saturate(outlineAlpha * _Opacity));
            }
            ENDHLSL
        }
    }
}
