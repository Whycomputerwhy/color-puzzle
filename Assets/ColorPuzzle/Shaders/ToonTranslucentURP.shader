Shader "ColorPuzzle/Toon Translucent URP"
{
    // CENTRALIZED ART-DIRECTION PARAMETERS
    Properties
    {
        [Header(Base Surface)]
        _Color("Tint and Opacity", Color) = (0.65, 0.9, 1, 0.3)
        _MainTex("Texture", 2D) = "white" {}

        [Header(Cel Lighting)]
        _RampLevels("Ramp Levels", Range(2, 5)) = 2
        _LightScalar("Light Scalar", Range(0, 3)) = 1
        _HighColor("Bright Face Tint", Color) = (1, 1, 1, 1)
        _HighIntensity("Bright Face Intensity", Range(0, 3)) = 1.1
        _LowColor("Shadow Face Tint", Color) = (0.55, 0.75, 0.85, 1)
        _LowIntensity("Shadow Face Intensity", Range(0, 3)) = 0.55

        [Header(Glass Edge)]
        _FresnelColor("Soft Edge Color", Color) = (0.8, 0.96, 1, 1)
        _FresnelBrightness("Soft Edge Brightness", Range(0, 2)) = 0.65
        _FresnelPower("Soft Edge Tightness", Range(1, 12)) = 4
        _HardRimColor("Hard Edge Color", Color) = (1, 1, 1, 1)
        _HardRimThreshold("Hard Edge Threshold", Range(0, 1)) = 0.78
        _HardRimBrightness("Hard Edge Brightness", Range(0, 2)) = 0.5
        _RimOpacity("Edge Opacity Boost", Range(0, 1)) = 0.4

        [Header(Rendering)]
        [Toggle] _DoubleSided("Double Sided", Float) = 1
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }

        Pass
        {
            Name "ToonTranslucent"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _Color, _MainTex_ST, _HighColor, _LowColor, _FresnelColor, _HardRimColor;
                float _RampLevels, _LightScalar, _HighIntensity, _LowIntensity;
                float _FresnelBrightness, _FresnelPower, _HardRimThreshold, _HardRimBrightness, _RimOpacity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.shadowCoord = GetShadowCoord(position);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Color;
                half3 normalWS = normalize(input.normalWS);
                half3 viewDirectionWS = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                Light mainLight = GetMainLight(input.shadowCoord);

                half lit = saturate(dot(normalWS, mainLight.direction) * _LightScalar);
                lit *= mainLight.shadowAttenuation;
                float steps = max(1.0, round(_RampLevels) - 1.0);
                half ramp = round(lit * steps) / steps;
                half3 celTint = lerp(_LowColor.rgb * _LowIntensity, _HighColor.rgb * _HighIntensity, ramp);
                half3 color = albedo.rgb * celTint;

                half edge = 1.0h - saturate(abs(dot(viewDirectionWS, normalWS)));
                half softRim = pow(edge, _FresnelPower) * _FresnelBrightness;
                half hardRim = step(_HardRimThreshold, edge) * _HardRimBrightness;
                color += _FresnelColor.rgb * softRim + _HardRimColor.rgb * hardRim;
                half alpha = saturate(albedo.a + (softRim + hardRim) * _RimOpacity);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
