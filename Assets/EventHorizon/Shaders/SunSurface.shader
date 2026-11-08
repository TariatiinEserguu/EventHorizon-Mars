// The Sun's photosphere: the Solar System Scope texture with slowly churning granulation (animated noise
// distortion) and limb darkening (the edge of the disc is cooler and dimmer, I = 1 - u(1 - mu), u ~ 0.6).
// Emissive HDR output; bloom gives a controlled glow rather than a blown-out disc.
Shader "EventHorizon/SunSurface"
{
    Properties
    {
        _MainTex ("Surface", 2D) = "white" {}
        [HDR] _Color ("Emission colour", Color) = (3.2, 2.6, 1.9, 1)
        _LimbDarkening ("Limb darkening u", Range(0, 1)) = 0.6
        _Churn ("Granulation motion", Range(0, 2)) = 0.6
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST, _Color;
                float _LimbDarkening, _Churn;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 normalWS : TEXCOORD1; float3 positionWS : TEXCOORD2; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = v.uv; o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y * _Churn;
                float2 w = float2(sin(i.uv.y * 40.0 + t * 0.7), cos(i.uv.x * 55.0 - t * 0.5)) * 0.0025;
                float3 a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + w).rgb;
                float3 b = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv * 1.0 - w * 1.7 + float2(t * 0.0004, 0)).rgb;
                float3 tex = lerp(a, b, 0.5 + 0.5 * sin(t * 0.3));
                float mu = saturate(dot(normalize(i.normalWS), normalize(GetCameraPositionWS() - i.positionWS)));
                float limb = 1.0 - _LimbDarkening * (1.0 - mu);
                return half4(tex * _Color.rgb * limb, 1);
            }
            ENDHLSL
        }
    }
}
