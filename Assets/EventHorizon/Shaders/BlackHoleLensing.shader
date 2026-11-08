// Schwarzschild black hole with a thin accretion disk, rendered by integrating light rays per pixel.
//
// Units: Schwarzschild radius r_s = 1, c = 1. In those units a photon's path obeys
//     d2x/dl2 = -1.5 * h^2 * x / |x|^5,   with h = |x cross dx/dl| conserved,
// the standard Cartesian form of the Schwarzschild null geodesic. From it you get the photon sphere at
// 1.5 r_s, the black shadow of radius ~2.6 r_s, and the lensed image of the disk's far side wrapping over the top.
//
// The disk is the object's local XZ plane between _DiskInner (ISCO = 3 r_s) and _DiskOuter.
// Temperature follows the thin-disk profile T ~ r^-3/4 (1 - sqrt(r_in/r))^1/4. Each disk hit is shifted by
// g = Doppler * gravitational redshift, and intensity scales as g^4, so the approaching side is brighter.
//
// Put it on a sphere (Unity's default sphere: radius 0.5 = _Bound r_s). Works with the camera inside or outside.
//
// Scientifically grounded: photon paths (Schwarzschild null geodesics), the shadow and photon ring, Einstein-ring
// lensing of the background, ISCO inner edge, Keplerian orbital speeds, Doppler beaming and gravitational redshift
// (g factor), and the thin-disk temperature profile.
// Artistic approximations: the gas texture (value noise), the disk thickness (infinitely thin), blackbody-to-RGB
// colour, the g^3 brightness boost (clamped so it doesn't blow out), and no light-travel-time effects. The black hole
// does not spin (Schwarzschild, not Kerr).
Shader "EventHorizon/BlackHoleLensing"
{
    Properties
    {
        [NoScaleOffset] _StarTex ("Background (equirectangular)", 2D) = "black" {}
        _StarExposure ("Background exposure", Float) = 1
        _Bound ("Sphere radius in r_s", Float) = 40
        _DiskInner ("Disk inner radius (r_s)", Float) = 3
        _DiskOuter ("Disk outer radius (r_s)", Float) = 14
        _DiskTemperature ("Disk peak temperature (K)", Float) = 4300
        _DiskBrightness ("Disk brightness", Float) = 3
        _DiskOpacity ("Disk opacity", Range(0, 1)) = 0.9
        _DopplerStrength ("Relativistic shift amount", Range(0, 1)) = 1
        _RotationSpeed ("Disk animation speed", Float) = 0.6
        _Steps ("Max integration steps", Range(32, 600)) = 300
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "BlackHole"
            Tags { "LightMode" = "UniversalForward" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_StarTex);
            SAMPLER(sampler_StarTex);

            CBUFFER_START(UnityPerMaterial)
                float _StarExposure;
                float _Bound;
                float _DiskInner;
                float _DiskOuter;
                float _DiskTemperature;
                float _DiskBrightness;
                float _DiskOpacity;
                float _DopplerStrength;
                float _RotationSpeed;
                float _Steps;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.positionOS = v.positionOS.xyz;
                return o;
            }

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x),
                            lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }

            // Gas density at disk point p after rotating it by angle a. Rotating the point (rather than using
            // atan2) keeps the noise free of a seam.
            float DiskNoise(float3 p, float a)
            {
                float sa, ca;
                sincos(a, sa, ca);
                float2 q = float2(ca * p.x - sa * p.z, sa * p.x + ca * p.z);
                return ValueNoise(q * 0.55) * 0.55 + ValueNoise(q * 1.6) * 0.3 + ValueNoise(q * 4.2) * 0.15;
            }

            // Approximate blackbody colour in linear RGB for 1000 K - 40000 K (Tanner Helland's fit).
            float3 Blackbody(float t)
            {
                t = clamp(t, 1000.0, 40000.0) / 100.0;
                float3 c;
                float hot = max(t - 60.0, 1.0);
                c.r = t <= 66.0 ? 1.0 : saturate(1.292936 * pow(hot, -0.1332048));
                c.g = t <= 66.0 ? saturate(0.3900816 * log(t) - 0.6318414) : saturate(1.129891 * pow(hot, -0.0755148));
                c.b = t >= 66.0 ? 1.0 : (t <= 19.0 ? 0.0 : saturate(0.5432068 * log(t - 10.0) - 1.1962541));
                return c * c; // sRGB-ish to linear
            }

            float3 Background(float3 dirOS)
            {
                float3 d = normalize(TransformObjectToWorldDir(dirOS));
                // Same mapping as Unity's Skybox/Panoramic, so the lensed stars line up with the real sky at the sphere edge.
                float2 uv = float2(0.5 - atan2(d.z, d.x) / (2.0 * PI), 0.5 + asin(clamp(d.y, -1.0, 1.0)) / PI);
                return SAMPLE_TEXTURE2D_LOD(_StarTex, sampler_StarTex, uv, 0).rgb * _StarExposure;
            }

            // Colour and opacity of the disk at hit point p (r_s units), for a ray travelling along rayDir.
            float4 Disk(float3 p, float3 rayDir)
            {
                float r = length(p.xz);
                if (r < _DiskInner || r > _DiskOuter) return float4(0, 0, 0, 0);

                // Differential (Keplerian) rotation: angular velocity sqrt(M / r^3) with M = 0.5 r_s, so the inner
                // disk laps the outer disk. Left running, that shear winds any pattern into ever-tighter rings, so two
                // samples at staggered phases are cross-faded (a flow-map trick) to keep the visible shear bounded.
                // [Artistic approximation: the gas texture. The rotation rates are physical.]
                float omega = sqrt(0.5 / (r * r * r));
                const float cycle = 10.0;
                float ph1 = frac(_Time.y / cycle), ph2 = frac(_Time.y / cycle + 0.5);
                float n1 = DiskNoise(p, -omega * ph1 * cycle * _RotationSpeed * 3.0);
                float n2 = DiskNoise(p, -omega * ph2 * cycle * _RotationSpeed * 3.0 + 1.7);
                float n = lerp(n1, n2, abs(ph1 * 2.0 - 1.0));
                float rings = 0.85 + 0.15 * sin(r * 3.0 + n * 6.0);
                float edges = smoothstep(_DiskInner, _DiskInner * 1.15, r) * (1.0 - smoothstep(_DiskOuter * 0.7, _DiskOuter, r));
                float density = saturate(n * rings * 1.6 - 0.1) * edges;

                float temperature = _DiskTemperature * 1.9 * pow(max(_DiskInner / r, 1e-4), 0.75) * pow(max(1.0 - sqrt(_DiskInner / r), 1e-4), 0.25);

                // Orbital speed seen by a static observer: v = sqrt(M / (r - 2M)), M = 0.5.
                float beta = min(sqrt(0.5 / max(r - 1.0, 1e-3)), 0.99);
                float gamma = rsqrt(1.0 - beta * beta);
                float3 velocityDir = normalize(float3(-p.z, 0.0, p.x));
                float cosTheta = dot(velocityDir, -rayDir); // photon travels opposite to our traced ray
                float doppler = 1.0 / (gamma * (1.0 - beta * cosTheta));
                float g = lerp(1.0, doppler * sqrt(saturate(1.0 - 1.0 / r)), _DopplerStrength);

                float3 emission = Blackbody(temperature * g) * _DiskBrightness * min(pow(max(g, 0.0), 3.0), 6.0);
                return float4(emission, density * _DiskOpacity);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float scale = _Bound / 0.5; // object-space sphere radius 0.5 -> _Bound r_s
                float3 camOS = TransformWorldToObject(GetCameraPositionWS());
                float3 pos = camOS * scale;
                float3 dir = normalize(i.positionOS - camOS);

                // If the camera is outside the sphere, march straight to its surface first: lensing is negligible out there.
                float R = _Bound;
                float b = dot(pos, dir);
                float c = dot(pos, pos) - R * R;
                if (c > 0.0)
                {
                    float disc = b * b - c;
                    if (disc < 0.0) return half4(Background(dir), 1);
                    pos += dir * max(0.0, -b - sqrt(disc));
                }

                float3 vel = dir;
                float3 hv = cross(pos, vel);
                float h2 = dot(hv, hv);

                float3 colour = 0;
                float alpha = 0;
                bool captured = false;
                int steps = (int)_Steps;

                [loop]
                for (int s = 0; s < 600; s++)
                {
                    if (s >= steps) break;
                    float r2 = dot(pos, pos);
                    float r = sqrt(r2);
                    if (r < 1.0) { captured = true; break; }
                    if (r > R * 1.01 && dot(pos, vel) > 0.0) break;

                    // Smaller steps near the hole, where the path bends most.
                    float dt = clamp(0.06 * r, 0.015, 1.0);
                    float3 prev = pos;
                    float3 acc = -1.5 * h2 * pos / (r2 * r2 * r);
                    vel += acc * dt;
                    pos += vel * dt;

                    // Crossing the disk plane (y = 0)?
                    if (prev.y * pos.y < 0.0 && alpha < 0.99)
                    {
                        float t = prev.y / (prev.y - pos.y);
                        float4 d = Disk(lerp(prev, pos, t), normalize(vel));
                        colour += (1.0 - alpha) * d.rgb * d.a;
                        alpha += (1.0 - alpha) * d.a;
                    }
                }

                float3 bg = captured ? 0 : Background(normalize(vel));
                return half4(colour + (1.0 - alpha) * bg, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
