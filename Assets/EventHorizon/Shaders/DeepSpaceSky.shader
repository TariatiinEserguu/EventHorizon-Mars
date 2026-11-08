// Deep-space skybox. Layers, from back to front:
//  1. A real Milky Way panorama (Solar System Scope, CC BY 4.0) at low exposure, so space stays mostly dark.
//  2. Procedural nebulae: domain-warped 3D noise concentrated toward a "galactic band", with emission tints
//     (H-alpha red, OIII teal) and dark molecular-cloud lanes that dim what is behind them.
// Sharp individual stars and distant galaxies are drawn separately by Starfield.cs as one instanced mesh, which
// keeps them crisp at any resolution. Each exploration region sets its own colours, density and seed.
Shader "EventHorizon/DeepSpaceSky"
{
    Properties
    {
        [NoScaleOffset] _MilkyWay ("Milky Way panorama", 2D) = "black" {}
        _MilkyWayExposure ("Milky Way exposure", Range(0, 2)) = 0.35
        _MilkyWayTint ("Milky Way tint", Color) = (1, 0.97, 0.92, 1)
        _Rotation ("Panorama rotation (deg)", Range(0, 360)) = 0

        _NebulaA ("Nebula colour A (emission)", Color) = (0.55, 0.12, 0.10, 1)
        _NebulaB ("Nebula colour B", Color) = (0.08, 0.32, 0.38, 1)
        _NebulaIntensity ("Nebula intensity", Range(0, 3)) = 0.5
        _NebulaScale ("Nebula scale", Range(0.5, 8)) = 2.2
        _NebulaSeed ("Nebula seed", Float) = 3.7
        _DustDarkness ("Dark cloud strength", Range(0, 1)) = 0.6
        _BandNormal ("Galactic band normal", Vector) = (0.25, 0.9, 0.35, 0)
        _BandWidth ("Band width", Range(0.05, 1.5)) = 0.45
        _Exposure ("Exposure", Range(0, 3)) = 1
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

            TEXTURE2D(_MilkyWay); SAMPLER(sampler_MilkyWay);
            CBUFFER_START(UnityPerMaterial)
                float _MilkyWayExposure, _Rotation, _NebulaIntensity, _NebulaScale, _NebulaSeed, _DustDarkness, _BandWidth, _Exposure;
                float4 _MilkyWayTint, _NebulaA, _NebulaB, _BandNormal;
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

            float Hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float Noise(float3 x)
            {
                float3 i = floor(x), f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(Hash(i), Hash(i + float3(1, 0, 0)), f.x),
                                 lerp(Hash(i + float3(0, 1, 0)), Hash(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(Hash(i + float3(0, 0, 1)), Hash(i + float3(1, 0, 1)), f.x),
                                 lerp(Hash(i + float3(0, 1, 1)), Hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

            float Fbm(float3 p)
            {
                float s = 0.0, a = 0.5;
                [unroll] for (int i = 0; i < 5; i++) { s += Noise(p) * a; p = p * 2.03 + 11.7; a *= 0.5; }
                return s;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.dir);

                // 1. Milky Way panorama (same mapping as Unity's Skybox/Panoramic), optionally rotated per region.
                float rot = radians(_Rotation);
                float3 dr = float3(d.x * cos(rot) - d.z * sin(rot), d.y, d.x * sin(rot) + d.z * cos(rot));
                float2 uv = float2(0.5 - atan2(dr.z, dr.x) / (2.0 * PI), 0.5 + asin(clamp(dr.y, -1.0, 1.0)) / PI);
                float3 milky = SAMPLE_TEXTURE2D_LOD(_MilkyWay, sampler_MilkyWay, uv, 0).rgb * _MilkyWayExposure * _MilkyWayTint.rgb;

                // 2. Nebulae: strongest near the galactic band, patchy elsewhere.
                float band = 1.0 - smoothstep(0.0, _BandWidth, abs(dot(d, normalize(_BandNormal.xyz))));
                float3 p = d * _NebulaScale + _NebulaSeed;
                float warp = Fbm(p);
                float n1 = Fbm(p * 1.7 + warp * 1.6);
                float n2 = Fbm(p * 2.9 - warp * 1.2 + 5.3);
                float dustN = Fbm(p * 3.6 + 9.1 + warp);

                float3 emission = _NebulaA.rgb * pow(smoothstep(0.48, 0.85, n1), 2.0)
                                + _NebulaB.rgb * pow(smoothstep(0.52, 0.88, n2), 2.0) * 0.8;
                float glow = smoothstep(0.35, 0.75, warp) * 0.15;            // faint diffuse interstellar glow
                float dust = smoothstep(0.45, 0.72, dustN) * _DustDarkness * (0.4 + 0.6 * band);

                float3 col = milky * (1.0 - dust)
                           + (emission * (0.25 + 0.75 * band) + glow * float3(0.35, 0.3, 0.28) * band) * _NebulaIntensity * (1.0 - dust * 0.7);
                return half4(col * _Exposure, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
