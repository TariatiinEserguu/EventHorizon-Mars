// Martian sky (skybox). An analytic approximation of how airborne dust scatters sunlight. It is not a full
// radiative-transfer solution, but it reproduces what the rovers photograph:
//  - Daytime: butterscotch/caramel sky. Micron-sized dust absorbs blue and scatters red broadly. Brighter and
//    more saturated toward the horizon, where the light path through the dust is longer.
//  - Sunset: a cool blue glow around the Sun. The dust scatters blue light forward (a narrow Mie lobe), so near
//    the Sun's direction blue wins (seen by Curiosity, InSight and Perseverance).
//  - Dust storms (higher optical depth tau): darker, flatter, redder sky with a dimmer Sun disc.
//  - Night: stars from the panorama, dimmed by dust.
// Inputs come from MarsEnvironment as globals: _EH_SunDirection (toward the Sun) and _EH_OpticalDepth.
Shader "EventHorizon/MarsSky"
{
    Properties
    {
        _ZenithColor ("Day zenith", Color) = (0.48, 0.33, 0.22, 1)
        _HorizonColor ("Day horizon", Color) = (0.86, 0.64, 0.45, 1)
        _GlowColor ("Sunset glow (forward-scattered blue)", Color) = (0.42, 0.6, 0.92, 1)
        _SunColor ("Sun disc", Color) = (1, 0.96, 0.88, 1)
        _SunSize ("Sun angular radius (deg)", Range(0.05, 2)) = 0.18
        _GroundColor ("Below horizon", Color) = (0.22, 0.13, 0.08, 1)
        [NoScaleOffset] _StarTex ("Night stars (equirectangular)", 2D) = "black" {}
        _StarIntensity ("Star brightness", Range(0, 3)) = 0.8
        _Exposure ("Exposure", Range(0.1, 4)) = 1.1
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_StarTex); SAMPLER(sampler_StarTex);
            float4 _EH_SunDirection;
            float _EH_OpticalDepth;

            CBUFFER_START(UnityPerMaterial)
                float4 _ZenithColor, _HorizonColor, _GlowColor, _SunColor, _GroundColor;
                float _SunSize, _StarIntensity, _Exposure;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.dir = v.positionOS.xyz;
                return o;
            }

            float HenyeyGreenstein(float cosT, float g)
            {
                float g2 = g * g;
                return (1.0 - g2) / (4.0 * PI * pow(max(1.0 + g2 - 2.0 * g * cosT, 1e-4), 1.5));
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float3 s = _EH_SunDirection.xyz;
                s = dot(s, s) > 0.01 ? normalize(s) : float3(0.3, 0.6, 0.74);
                float tau = _EH_OpticalDepth > 0.0 ? _EH_OpticalDepth : 0.5;

                float elev = s.y;                                   // sin(sun elevation)
                float day = saturate(elev * 5.0 + 0.35);            // fades through civil twilight
                float h = saturate(d.y);
                float cosT = dot(d, s);

                // Base sky: longer slant path near the horizon means brighter, more saturated dust.
                float3 sky = lerp(_HorizonColor.rgb, _ZenithColor.rgb, pow(h, 0.45));
                float stormy = saturate((tau - 0.5) / 4.0);
                sky = lerp(sky, float3(0.5, 0.32, 0.2), stormy * 0.6) * lerp(1.0, 0.55, stormy);

                // Near-sun forward scattering: warm when the Sun is high, blue at low Sun (the Martian sunset).
                float lowSun = saturate(1.0 - elev * 3.0);
                float lobe = HenyeyGreenstein(cosT, 0.82) * 2.2 + HenyeyGreenstein(cosT, 0.35) * 0.6;
                float3 glowCol = lerp(float3(1.0, 0.82, 0.62), _GlowColor.rgb, lowSun);
                float3 glow = glowCol * lobe * (1.0 - stormy * 0.7);

                float3 col = (sky * (0.35 + 0.65 * saturate(elev * 2.0 + 0.2)) + glow) * day;

                // Sun disc, dimmed by the slant optical depth.
                float sunCos = cos(radians(_SunSize));
                float disc = smoothstep(sunCos - 0.00012, sunCos + 0.00012, cosT);
                float extinction = exp(-tau / max(elev, 0.05));
                col += _SunColor.rgb * disc * extinction * 30.0 * step(-0.02, elev);

                // Night: stars, dimmed by dust and by twilight.
                float night = saturate(-elev * 6.0 + 0.2);
                float2 uv = float2(0.5 - atan2(d.z, d.x) / (2.0 * PI), 0.5 + asin(clamp(d.y, -1.0, 1.0)) / PI);
                float3 stars = SAMPLE_TEXTURE2D_LOD(_StarTex, sampler_StarTex, uv, 0).rgb;
                col += stars * _StarIntensity * night * exp(-tau * 0.5) * smoothstep(-0.05, 0.1, d.y);

                // Below the horizon: dark ground haze.
                float below = saturate(-d.y * 8.0);
                col = lerp(col, _GroundColor.rgb * (0.15 + 0.85 * day), below);

                return half4(col * _Exposure, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
