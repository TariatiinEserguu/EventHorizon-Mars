// Distant galaxies: small world-space quads on the sky sphere with a soft elliptical (Sersic-like) profile, a
// bright core and a hint of spiral structure. Additive and very faint, as real galaxies are to the eye.
Shader "EventHorizon/Galaxy"
{
    Properties
    {
        _Intensity ("Intensity", Range(0, 3)) = 0.6
    }
    SubShader
    {
        Tags { "Queue" = "Transparent-460" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Intensity;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; float2 shape : TEXCOORD1; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 color : TEXCOORD0; float2 uv : TEXCOORD1; float2 shape : TEXCOORD2; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = v.color; o.uv = v.uv; o.shape = v.shape;
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                // shape.x = axis ratio (inclination), shape.y = spiral seed
                float2 p = i.uv;
                p.y /= max(i.shape.x, 0.15);
                float r = length(p);
                float a = atan2(p.y, p.x);
                float arms = 0.65 + 0.35 * sin(2.0 * a + log(r + 0.05) * 6.0 + i.shape.y);
                float disk = exp(-r * 4.5) * arms;
                float bulge = exp(-r * r * 60.0) * 1.6;
                float edge = saturate(1.0 - r);
                float3 c = i.color.rgb * (disk + bulge) * edge * i.color.a * _Intensity;
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
}
