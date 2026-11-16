Shader "ColorPuzzle/Underwater Gradient Backdrop"
{
    Properties
    {
        _UpperColor("Upper Water", Color) = (0.10, 0.42, 0.56, 1)
        _LowerColor("Lower Water", Color) = (0.025, 0.12, 0.22, 1)
        _VerticalBlendPower("Vertical Blend Power", Range(0.25, 4)) = 1.15
        [HideInInspector] _ColorPuzzleSphereRevealProxy("Sphere Reveal Proxy", Float) = 0
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
                float3 positionWS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _UpperColor;
                float4 _LowerColor;
                float _VerticalBlendPower;
                float _ColorPuzzleSphereRevealProxy;
            CBUFFER_END

            float _ColorPuzzleSphereRevealEnabled;
            float4 _ColorPuzzleSphereRevealOriginRadius;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // The backdrop sits far behind the playable space. Testing the
                // backdrop plane itself delays its reveal until well after
                // nearby models. Instead, project the same world-space sphere
                // along this pixel's camera ray, which makes the background
                // boundary coincide with the visible edge of the 3D reveal.
                if (_ColorPuzzleSphereRevealProxy > 0.5 && _ColorPuzzleSphereRevealEnabled > 0.5)
                {
                    float3 cameraPosition = GetCameraPositionWS();
                    float3 rayDirection = normalize(input.positionWS - cameraPosition);
                    float3 toSphere = _ColorPuzzleSphereRevealOriginRadius.xyz - cameraPosition;
                    float nearestDistance = max(0.0, dot(toSphere, rayDirection));
                    float distanceFromRay = length(toSphere - rayDirection * nearestDistance);
                    clip(_ColorPuzzleSphereRevealOriginRadius.w - distanceFromRay);
                }
                float verticalBlend = pow(saturate(input.uv.y), _VerticalBlendPower);
                return lerp(_LowerColor, _UpperColor, verticalBlend);
            }
            ENDHLSL
        }
    }
}
