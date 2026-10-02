Shader "ColorPuzzle/UI/ValueGreyscale"
{
    Properties
    {
        [PerRendererData] _MainTex ("Screen Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            struct Attributes
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct Varyings
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color * _Color;
                return output;
            }

            fixed4 Frag(Varyings input) : SV_Target
            {
                fixed4 source = tex2D(_MainTex, input.uv) * input.color;
                // Rec. 709 luminance preserves the intrinsic value relationship between hues.
                fixed value = dot(source.rgb, fixed3(0.2126, 0.7152, 0.0722));
                return fixed4(value, value, value, source.a);
            }
            ENDCG
        }
    }
}
