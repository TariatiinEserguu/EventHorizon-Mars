// Planet surface, lit by the Sun's actual position (_EH_SunPosition, set by SunLight.cs), so every planet in a
// scene gets the correct day side even though URP has only one directional light.
// Features: normal-mapped relief, soft terminator, night-side emission (Earth's city lights), ocean specular
// (Earth), atmospheric scattering at the limb and terminator, and the shadow of a ring system (Saturn).
// Real-time atmospheric ray marching is deliberately avoided: the atmosphere is an analytic approximation.
Shader "EventHorizon/Planet"
{
    Properties
    {
        _MainTex ("Albedo", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        [Normal][NoScaleOffset] _NormalMap ("Normal map", 2D) = "bump" {}
        _NormalStrength ("Relief strength", Range(0, 3)) = 1
        [NoScaleOffset] _NightTex ("Night emission (city lights)", 2D) = "black" {}
        _NightIntensity ("Night intensity", Range(0, 4)) = 0
        [NoScaleOffset] _SpecMask ("Specular mask (oceans)", 2D) = "black" {}
        _Specular ("Specular strength", Range(0, 2)) = 0
        _AtmoColor ("Atmosphere colour", Color) = (0.35, 0.55, 1, 1)
        _AtmoTwilight ("Twilight colour", Color) = (1, 0.45, 0.2, 1)
        _AtmoStrength ("Atmosphere strength", Range(0, 3)) = 0
        _Wrap ("Terminator softness", Range(0, 0.5)) = 0.08
        _RingInner ("Ring inner radius (planet radii, 0 = none)", Float) = 0
        _RingOuter ("Ring outer radius", Float) = 0
        [NoScaleOffset] _RingTex ("Ring texture (alpha = opacity)", 2D) = "black" {}
        _Ambient ("Ambient", Range(0, 0.2)) = 0.012
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Planet"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            TEXTURE2D(_NightTex); SAMPLER(sampler_NightTex);
            TEXTURE2D(_SpecMask); SAMPLER(sampler_SpecMask);
            TEXTURE2D(_RingTex); SAMPLER(sampler_RingTex);
            float4 _EH_SunPosition; // xyz world position, w = 1 when valid

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST, _Color, _AtmoColor, _AtmoTwilight;
                float _NormalStrength, _NightIntensity, _Specular, _AtmoStrength, _Wrap, _RingInner, _RingOuter, _Ambient;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 tangentWS : TEXCOORD3;
                float3 positionOS : TEXCOORD4;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.tangentWS = float4(TransformObjectToWorldDir(v.tangentOS.xyz), v.tangentOS.w);
                o.positionOS = v.positionOS.xyz;
                return o;
            }

            float3 SunDir(float3 posWS)
            {
                if (_EH_SunPosition.w > 0.5) return normalize(_EH_SunPosition.xyz - posWS);
                return normalize(GetMainLight().direction);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 ng = normalize(i.normalWS);
                float3 t = normalize(i.tangentWS.xyz);
                float3 b = cross(ng, t) * i.tangentWS.w;
                float3 nt = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, i.uv), _NormalStrength);
                float3 n = normalize(nt.x * t + nt.y * b + nt.z * ng);

                float3 L = SunDir(i.positionWS);
                float3 V = normalize(GetCameraPositionWS() - i.positionWS);
                float ndlGeo = dot(ng, L);
                float day = saturate((ndlGeo + _Wrap) / (1.0 + _Wrap));
                float lit = saturate(dot(n, L)) * smoothstep(-_Wrap, _Wrap * 2.0 + 0.02, ndlGeo) + day * 0.15;

                // Ring shadow: march from this point toward the Sun and see whether the ray crosses the ring plane.
                float shadow = 1.0;
                if (_RingOuter > 0.0)
                {
                    float3 lo = normalize(TransformWorldToObjectDir(L));
                    float3 po = i.positionOS;
                    if (abs(lo.y) > 1e-4)
                    {
                        float tt = -po.y / lo.y;
                        if (tt > 0.0)
                        {
                            float r = length((po + lo * tt).xz);
                            if (r > _RingInner && r < _RingOuter)
                            {
                                float u = (r - _RingInner) / (_RingOuter - _RingInner);
                                shadow = 1.0 - SAMPLE_TEXTURE2D_LOD(_RingTex, sampler_RingTex, float2(u, 0.5), 0).a * 0.85;
                            }
                        }
                    }
                }

                float3 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).rgb * _Color.rgb;
                float3 col = albedo * (lit * shadow + _Ambient);

                // Oceans: sun glint.
                float spec = SAMPLE_TEXTURE2D(_SpecMask, sampler_SpecMask, i.uv).r * _Specular;
                float3 h = normalize(L + V);
                col += spec * pow(saturate(dot(ng, h)), 300.0) * day * shadow * float3(1.0, 0.95, 0.85);

                // Night side: city lights fade in past the terminator.
                float night = smoothstep(0.05, -0.2, ndlGeo);
                col += SAMPLE_TEXTURE2D(_NightTex, sampler_NightTex, i.uv).rgb * _NightIntensity * night;

                // Atmosphere: limb brightening on the day side, warm scattering at the terminator.
                float fres = pow(1.0 - saturate(dot(ng, V)), 3.0);
                float twilight = saturate(1.0 - abs(ndlGeo) * 5.0);
                col += (_AtmoColor.rgb * fres * saturate(ndlGeo * 1.3 + 0.25) + _AtmoTwilight.rgb * twilight * 0.18 * fres) * _AtmoStrength;
                col = lerp(col, col + _AtmoColor.rgb * 0.06 * day * _AtmoStrength, 0.5);
                return half4(col, 1);
            }
            ENDHLSL
        }
        // Depth pre-pass, so ambient occlusion and depth-based effects see the planets.
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex v
            #pragma fragment f
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 v(float4 p : POSITION) : SV_POSITION { return TransformObjectToHClip(p.xyz); }
            half f() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
