// Black hole with a thin accretion disk, rendered by integrating a light ray per pixel (GPU, no hardware RT).
//
// Units: Schwarzschild radius r_s = 1, c = 1, so M = 0.5. Light paths use the Cartesian form of the Schwarzschild
// null geodesic,
//     d2x/dl2 = -1.5 * h^2 * x / |x|^5,   h = |x cross dx/dl|,
// which gives the photon sphere at 1.5 r_s, the shadow of radius ~2.6 r_s, Einstein rings and the lensed far side of
// the disk wrapping over the top.
//
// Spin (Kerr), approximated:
//  - rays are captured at the Kerr outer horizon r+ = M (1 + sqrt(1 - a^2)) instead of r_s;
//  - frame dragging adds the weak-field gravitomagnetic term a_gm = -2 v x B, B = (3 (J.n) n - J) / r^3 with
//    J = a M^2 along the spin axis. Co-rotating light is pushed outward, counter-rotating light inward, so the
//    shadow shifts and flattens on one side like a Kerr shadow. Exact Kerr geodesics (Carter constant) are not
//    integrated; the lab's readouts state this.
//
// Disk: the object's local XZ plane between _DiskInner (the ISCO) and _DiskOuter, rotating about -Y (prograde with the
// hole). Temperature follows the thin-disk profile T ~ r^-3/4 (1 - sqrt(r_in/r))^1/4. Each disk hit is shifted by the
// exact Kerr circular-orbit factor g = 1 / (u^t (1 - Omega lambda)), with u^t and Omega for an equatorial circular
// orbit and lambda the photon's angular momentum (from the ray at the camera). That one factor holds Doppler boosting
// and dimming and gravitational redshift; intensity scales as g^3 (clamped).
//
// Artistic approximations: the gas texture (value noise), an infinitely thin disk, blackbody-to-RGB colour, the
// brightness clamp, no light-travel-time effects, and the "visual temperature" (real disks of stellar-mass holes emit
// X-rays at ~10 million K, which would look uniformly blue-white).
//
// Put it on a sphere (Unity's default sphere: radius 0.5 = _Bound r_s). Works with the camera inside or outside.
Shader "EventHorizon/BlackHoleLensing"
{
    Properties
    {
        [NoScaleOffset] _StarTex ("Background (equirectangular)", 2D) = "black" {}
        _StarExposure ("Background exposure", Float) = 1
        _Bound ("Sphere radius in r_s", Float) = 40
        _HorizonR ("Horizon radius (r_s)", Float) = 1
        _DiskInner ("Disk inner radius (r_s)", Float) = 3
        _DiskOuter ("Disk outer radius (r_s)", Float) = 14
        _DiskTemperature ("Disk peak temperature (K, visual)", Float) = 4300
        _DiskBrightness ("Disk brightness", Float) = 3
        _DiskDensity ("Disk density", Range(0, 1)) = 0.8
        _DiskOpacity ("Disk opacity", Range(0, 1)) = 0.9
        _DopplerStrength ("Relativistic shift amount", Range(0, 1)) = 1
        _RotationSpeed ("Disk animation speed", Float) = 0.6
        _SimTime ("Simulation time (set by script)", Float) = 0
        _Steps ("Max integration steps", Range(32, 600)) = 300
        _StepScale ("Step size factor", Range(0.5, 2.5)) = 1
        _Detail ("Gas detail (octaves)", Range(1, 3)) = 3
        _Spin ("Spin a", Range(0, 0.998)) = 0
        _FrameDrag ("Frame dragging of light", Range(0, 1)) = 1
        _SourceDir ("Background star direction (world)", Vector) = (0, 0, 1, 0)
        _SourceSize ("Background star angular size (rad)", Float) = 0.01
        _SourceBrightness ("Background star brightness", Float) = 0
        _HotSpotR ("Hot spot orbit radius (r_s)", Float) = 3
        _HotSpotStrength ("Hot spot brightness", Float) = 0
        _HotSpotOmega ("Hot spot angular speed", Float) = 0.2
    }

    SubShader
    {
        // Drawn after the star field: where lensing is weak the sphere fades out and the real sky shows through,
        // so the sphere's edge is invisible.
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-100" }

        Pass
        {
            Name "BlackHole"
            Tags { "LightMode" = "UniversalForward" }
            Cull Front
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

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
                float _HorizonR;
                float _DiskInner;
                float _DiskOuter;
                float _DiskTemperature;
                float _DiskBrightness;
                float _DiskDensity;
                float _DiskOpacity;
                float _DopplerStrength;
                float _RotationSpeed;
                float _SimTime;
                float _Steps;
                float _StepScale;
                float _Detail;
                float _Spin;
                float _FrameDrag;
                float4 _SourceDir;
                float _SourceSize;
                float _SourceBrightness;
                float _HotSpotR;
                float _HotSpotStrength;
                float _HotSpotOmega;
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

            // Gas density at disk point p after rotating it by angle a (rotating the point avoids an atan2 seam).
            float DiskNoise(float3 p, float a)
            {
                float sa, ca;
                sincos(a, sa, ca);
                float2 q = float2(ca * p.x - sa * p.z, sa * p.x + ca * p.z);
                float n = ValueNoise(q * 0.55) * 0.55;
                if (_Detail > 1.5) n += ValueNoise(q * 1.6) * 0.3; else n += 0.15;
                if (_Detail > 2.5) n += ValueNoise(q * 4.2) * 0.15; else n += 0.07;
                return n;
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

            // Sky in direction dirOS (object space): the star map plus the movable background star.
            float3 Background(float3 dirOS, out float sourceAmount)
            {
                float3 d = normalize(TransformObjectToWorldDir(dirOS));
                // Same mapping as Unity's Skybox/Panoramic, so the lensed stars line up with the real sky at the sphere edge.
                float2 uv = float2(0.5 - atan2(d.z, d.x) / (2.0 * PI), 0.5 + asin(clamp(d.y, -1.0, 1.0)) / PI);
                float3 sky = SAMPLE_TEXTURE2D_LOD(_StarTex, sampler_StarTex, uv, 0).rgb * _StarExposure;
                float ang = acos(clamp(dot(d, normalize(_SourceDir.xyz)), -1.0, 1.0));
                sourceAmount = _SourceBrightness > 0.0 ? exp(-(ang * ang) / max(_SourceSize * _SourceSize, 1e-8)) : 0.0;
                return sky + sourceAmount * _SourceBrightness * float3(1.0, 0.93, 0.82);
            }

            // Colour and opacity of the disk at hit point p (r_s units). lambdaM: the photon's angular momentum per
            // unit energy about the spin axis, in units of M, positive when the light moves with the disk.
            float4 Disk(float3 p, float lambdaM)
            {
                float r = length(p.xz);
                if (r < _DiskInner || r > _DiskOuter) return float4(0, 0, 0, 0);

                // Differential (Keplerian) rotation: inner rings lap outer ones. Two samples at staggered phases are
                // cross-faded (a flow-map trick) to keep the visible shear bounded. [Artistic: the gas texture.]
                float omega = sqrt(0.5 / (r * r * r));
                const float cycle = 10.0;
                float t = _SimTime;
                float ph1 = frac(t / cycle), ph2 = frac(t / cycle + 0.5);
                float n1 = DiskNoise(p, -omega * ph1 * cycle * _RotationSpeed * 3.0);
                float n2 = DiskNoise(p, -omega * ph2 * cycle * _RotationSpeed * 3.0 + 1.7);
                float n = lerp(n1, n2, abs(ph1 * 2.0 - 1.0));
                float rings = 0.85 + 0.15 * sin(r * 3.0 + n * 6.0);
                float edges = smoothstep(_DiskInner, _DiskInner * 1.15, r) * (1.0 - smoothstep(_DiskOuter * 0.7, _DiskOuter, r));
                float density = saturate(n * rings * (0.6 + _DiskDensity) - (1.0 - _DiskDensity) * 0.45) * edges;

                // An orbiting hot spot (a clump of hot gas) at _HotSpotR, moving with the disk.
                float spotAngle = _SimTime * _HotSpotOmega;
                float2 spot = _HotSpotR * float2(cos(spotAngle), sin(spotAngle));
                float hot = _HotSpotStrength * exp(-dot(p.xz - spot, p.xz - spot) / 0.35);

                float temperature = _DiskTemperature * 1.9 * pow(max(_DiskInner / r, 1e-4), 0.75) * pow(max(1.0 - sqrt(_DiskInner / r), 1e-4), 0.25);

                // Kerr equatorial circular orbit, M = 1 units: r_M = 2 r.
                float rM = 2.0 * r;
                float a = _Spin;
                float rs15 = rM * sqrt(rM);
                float denom = max(rs15 - 3.0 * sqrt(rM) + 2.0 * a, 1e-3);
                float ut = (rs15 + a) / (pow(rM, 0.75) * sqrt(denom));
                float Omega = 1.0 / (rs15 + a);
                float g = 1.0 / max(ut * (1.0 - Omega * lambdaM), 1e-3);
                g = lerp(1.0, g, _DopplerStrength);

                float3 emission = Blackbody(temperature * g) * _DiskBrightness * min(pow(max(g, 0.0), 3.0), 6.0) * (1.0 + hot);
                return float4(emission, saturate(density * _DiskOpacity + hot * 0.5));
            }

            half4 frag(Varyings i) : SV_Target
            {
                float scale = _Bound / 0.5; // object-space sphere radius 0.5 -> _Bound r_s
                float3 camOS = TransformWorldToObject(GetCameraPositionWS());
                float3 pos = camOS * scale;
                float3 dir = normalize(i.positionOS - camOS);
                float src;

                // If the camera is outside the sphere, march straight to its surface first: lensing is negligible out there.
                float R = _Bound;
                float b = dot(pos, dir);
                float c = dot(pos, pos) - R * R;
                if (c > 0.0)
                {
                    float disc = b * b - c;
                    if (disc < 0.0) return half4(Background(dir, src), 1);
                    pos += dir * max(0.0, -b - sqrt(disc));
                }

                float3 vel = dir;
                float3 dir0 = dir;
                float3 hv = cross(pos, vel);
                float h2 = dot(hv, hv);
                // Photon angular momentum about the spin axis (-Y), in units of M (r_s = 2M).
                float lambdaM = 2.0 * hv.y;
                float3 J = float3(0.0, -0.25 * _Spin, 0.0); // J = a M^2 along the spin axis

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
                    if (r < _HorizonR) { captured = true; break; }
                    if (r > R * 1.01 && dot(pos, vel) > 0.0) break;

                    // Smaller steps near the hole, where the path bends most.
                    float dt = clamp(0.06 * r, 0.015, 1.0) * _StepScale;
                    float3 prev = pos;
                    float3 acc = -1.5 * h2 * pos / (r2 * r2 * r);
                    if (_Spin > 0.0)
                    {
                        float3 n = pos / r;
                        float3 B = (3.0 * dot(J, n) * n - J) / (r2 * r);
                        acc += -2.0 * cross(vel, B) * _FrameDrag;
                    }
                    vel += acc * dt;
                    pos += vel * dt;

                    // Crossing the disk plane (y = 0)?
                    if (prev.y * pos.y < 0.0 && alpha < 0.99)
                    {
                        float t = prev.y / (prev.y - pos.y);
                        float4 d = Disk(lerp(prev, pos, t), lambdaM);
                        colour += (1.0 - alpha) * d.rgb * d.a;
                        alpha += (1.0 - alpha) * d.a;
                    }
                }

                src = 0;
                float3 bg = captured ? 0 : Background(normalize(vel), src);
                float deflection = acos(clamp(dot(normalize(vel), dir0), -1.0, 1.0));
                float coverage = captured ? 1.0 : max(max(alpha, smoothstep(0.04, 0.22, deflection)), saturate(src * 4.0));
                return half4(colour + (1.0 - alpha) * bg, coverage);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
