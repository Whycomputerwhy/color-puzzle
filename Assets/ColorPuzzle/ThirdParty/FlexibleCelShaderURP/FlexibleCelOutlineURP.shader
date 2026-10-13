Shader "ColorPuzzle/Flexible Cel URP/Outline"
{
    // CENTRALIZED ART-DIRECTION PARAMETERS
    Properties
    {
        [Header(Base Surface)]
        _Color("Global Color Modifier", Color) = (1, 1, 1, 1)
        _MainTex("Texture", 2D) = "white" {}
        _NormalTex("Normal", 2D) = "bump" {}
        _EmmisTex("Emission", 2D) = "black" {}

        [Header(Cel Lighting)]
        _RampLevels("Ramp Levels", Range(2, 50)) = 2
        _LightScalar("Light Scalar", Range(0, 10)) = 1
        _HighColor("High Light Color", Color) = (1, 1, 1, 1)
        _HighIntensity("High Light Intensity", Range(0, 10)) = 1.5
        _LowColor("Low Light Color", Color) = (1, 1, 1, 1)
        _LowIntensity("Low Light Intensity", Range(0, 10)) = 1

        [Header(Outline)]
        _OutlineColor("Outline Color", Color) = (0, 0, 0, 1)
        _OutlineSize("Outline Size", Range(0, 30)) = 1

        [Header(Edge Lighting)]
        _RimColor("Hard Edge Light Color", Color) = (1, 1, 1, 1)
        _RimAlpha("Hard Edge Light Brightness", Range(0, 1)) = 0
        _RimPower("Hard Edge Light Size", Range(0, 1)) = 0
        _RimDropOff("Hard Edge Light Dropoff", Range(0, 1)) = 0
        _FresnelColor("Soft Edge Light Color", Color) = (1, 1, 1, 1)
        _FresnelBrightness("Soft Edge Light Brightness", Range(0, 1)) = 0
        _FresnelPower("Soft Edge Light Size", Range(0, 1)) = 0
        _FresnelShadowDropoff("Soft Edge Light Dropoff", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Back

            HLSLPROGRAM
            #pragma vertex CelVertex
            #pragma fragment CelFragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_NormalTex); SAMPLER(sampler_NormalTex);
            TEXTURE2D(_EmmisTex); SAMPLER(sampler_EmmisTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _Color, _MainTex_ST, _NormalTex_ST, _EmmisTex_ST;
                float4 _HighColor, _LowColor, _OutlineColor, _RimColor, _FresnelColor;
                float _RampLevels, _LightScalar, _HighIntensity, _LowIntensity, _OutlineSize;
                float _RimAlpha, _RimPower, _RimDropOff;
                float _FresnelBrightness, _FresnelPower, _FresnelShadowDropoff;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half3 tangentWS : TEXCOORD3;
                half3 bitangentWS : TEXCOORD4;
                float4 shadowCoord : TEXCOORD5;
            };

            Varyings CelVertex(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.uv = input.uv;
                output.normalWS = normalInputs.normalWS;
                output.tangentWS = normalInputs.tangentWS;
                output.bitangentWS = normalInputs.bitangentWS;
                output.shadowCoord = GetShadowCoord(positionInputs);
                return output;
            }

            half4 CelFragment(Varyings input) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, TRANSFORM_TEX(input.uv, _MainTex));
                half3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_NormalTex, sampler_NormalTex,
                    TRANSFORM_TEX(input.uv, _NormalTex)));
                half3 normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(normalTS,
                    half3x3(input.tangentWS, input.bitangentWS, input.normalWS)));
                half3 viewDirectionWS = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));
                Light mainLight = GetMainLight(input.shadowCoord);

                half lit = saturate(dot(normalWS, mainLight.direction) * _LightScalar);
                lit *= mainLight.shadowAttenuation;
                float rampSteps = max(1.0, round(_RampLevels) - 1.0);
                half ramp = round(lit * rampSteps) / rampSteps;
                half3 rampTint = lerp(_LowColor.rgb, _HighColor.rgb, ramp) * 0.5;
                half3 color = albedo.rgb * _Color.rgb * rampTint * lerp(_LowIntensity, _HighIntensity, ramp);

                half edge = 1.0h - saturate(dot(viewDirectionWS, normalWS));
                half softRim = pow(edge, lerp(10.0h, 1.0h, _FresnelPower));
                color += _FresnelColor.rgb * softRim * _FresnelBrightness *
                    lerp(1.0h - _FresnelShadowDropoff, 1.0h, ramp);

                half hardRim = step(saturate(dot(viewDirectionWS, normalWS)), _RimPower);
                color = lerp(color, _RimColor.rgb, hardRim * _RimAlpha *
                    lerp(1.0h - _RimDropOff, 1.0h, ramp));

                half3 emission = SAMPLE_TEXTURE2D(_EmmisTex, sampler_EmmisTex,
                    TRANSFORM_TEX(input.uv, _EmmisTex)).rgb;
                color = lerp(color, emission, max(emission.r, max(emission.g, emission.b)));
                return half4(color, albedo.a * _Color.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex OutlineVertex
            #pragma fragment OutlineFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineSize;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings OutlineVertex(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                positionWS += TransformObjectToWorldNormal(input.normalOS) * (_OutlineSize * 0.001);
                output.positionCS = TransformWorldToHClip(positionWS);
                return output;
            }

            half4 OutlineFragment(Varyings input) : SV_Target { return _OutlineColor; }
            ENDHLSL
        }

        // Reuse URP's standard geometry-only passes. The custom forward pass above
        // samples shadow attenuation; this makes cel-shaded meshes also write into
        // the main-light shadow map so they can cast onto one another.
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
