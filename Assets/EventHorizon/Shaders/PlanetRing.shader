// Planetary ring (Saturn). U runs inner to outer edge. Lit by the Sun's position; the planet's shadow falls on
// the rings (ray from the ring point toward the Sun tested against the unit sphere in object space), and the rings
// glow brighter when backlit (forward scattering by ice grains).
Shader "EventHorizon/PlanetRing"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Ring texture (rgb colour, a opacity)", 2D) = "white" {}
        _Brightness ("Brightness", Range(0, 3)) = 1.1
        _Forward ("Backlit forward scattering", Range(0, 3)) = 1.2
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float4 _EH_SunPosition;
            CBUFFER_START(UnityPerMaterial)
                float _Brightness, _Forward;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; float3 positionOS : TEXCOORD2; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = v.uv; o.positionOS = v.positionOS.xyz;
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, float2(i.uv.x, 0.5));
                float3 L = _EH_SunPosition.w > 0.5 ? normalize(_EH_SunPosition.xyz - i.positionWS) : normalize(GetMainLight().direction);
                float3 V = normalize(GetCameraPositionWS() - i.positionWS);
                float3 lo = normalize(TransformWorldToObjectDir(L));
                // Planet shadow: does the ray toward the Sun hit the planet (radius 1 in object space)?
                float3 p = i.positionOS;
                float bq = dot(p, lo);
                float cq = dot(p, p) - 1.0;
                float disc = bq * bq - cq;
                float shadow = (disc > 0.0 && -bq - sqrt(disc) > 0.0) ? 0.08 : 1.0;
                float facing = abs(lo.y) * 0.6 + 0.4;
                float backlit = pow(saturate(dot(-V, L)), 6.0) * _Forward;
                float3 col = tex.rgb * (facing + backlit) * _Brightness * shadow;
                return half4(col, tex.a);
            }
            ENDHLSL
        }
    }
}
