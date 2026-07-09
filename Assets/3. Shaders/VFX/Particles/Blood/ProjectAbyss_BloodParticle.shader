Shader "ProjectAbyss/VFX/Particles/BloodParticle"
{
    Properties
    {
        _TintColor ("Blood Color", Color) = (0.55, 0.0, 0.0, 1.0)
        _DarkColor ("Dark Blood Color", Color) = (0.12, 0.0, 0.0, 1.0)

        _Intensity ("Intensity", Float) = 1.0

        _NoiseScale ("Noise Scale", Float) = 5.5
        _NoiseAmount ("Noise Amount", Range(0.0, 0.8)) = 0.28

        _EdgeSharpness ("Edge Sharpness", Float) = 1.8
        _AlphaPower ("Alpha Power", Float) = 0.85

        [Enum(UnityEngine.Rendering.CullMode)]
        _Cull ("Cull", Float) = 0

        [Enum(UnityEngine.Rendering.CompareFunction)]
        _ZTest ("ZTest", Float) = 4
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest [_ZTest]
        Cull [_Cull]
        Lighting Off

        Pass
        {
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "BloodParticleCore.hlsl"

            float4 _TintColor;
            float4 _DarkColor;

            float _Intensity;

            float _NoiseScale;
            float _NoiseAmount;

            float _EdgeSharpness;
            float _AlphaPower;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;

                o.position =
                    UnityObjectToClipPos(v.vertex);

                o.uv =
                    v.uv;

                o.color =
                    v.color;

                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                return BloodParticleFragCore(
                    i.uv,
                    i.color,
                    _TintColor,
                    _DarkColor,
                    _Time.y,
                    _NoiseScale,
                    _NoiseAmount,
                    _EdgeSharpness,
                    _AlphaPower,
                    _Intensity);
            }

            ENDHLSL
        }
    }

    FallBack Off
}