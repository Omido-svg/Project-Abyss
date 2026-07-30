Shader "ProjectAbyss/VFX/Particles/FireParticle"
{
    Properties
    {
        _TintColor ("Tint Color", Color) = (1.0, 0.25, 0.03, 1.0)
        _HotColor ("Hot Core Color", Color) = (1.0, 0.85, 0.15, 1.0)

        _Intensity ("Intensity", Float) = 1.5

        _NoiseScale ("Noise Scale", Float) = 4.5
        _NoiseSpeed ("Noise Speed", Float) = 2.5

        _CorePower ("Core Power", Float) = 1.7
        _EdgeSoftness ("Edge Softness", Range(0.01, 0.8)) = 0.35
        _Distortion ("Distortion", Range(0.0, 0.8)) = 0.22
        _AlphaPower ("Alpha Power", Float) = 1.2

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

        Blend SrcAlpha One
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
            #include "FireParticleCore.hlsl"

            float4 _TintColor;
            float4 _HotColor;

            float _Intensity;

            float _NoiseScale;
            float _NoiseSpeed;

            float _CorePower;
            float _EdgeSoftness;
            float _Distortion;
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
                return FireParticleFragCore(
                    i.uv,
                    i.color,
                    _TintColor,
                    _HotColor,
                    _Time.y,
                    _NoiseScale,
                    _NoiseSpeed,
                    _CorePower,
                    _EdgeSoftness,
                    _Distortion,
                    _AlphaPower,
                    _Intensity);
            }

            ENDHLSL
        }
    }

    FallBack Off
}