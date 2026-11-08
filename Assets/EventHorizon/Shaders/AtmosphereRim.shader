// Thin-atmosphere limb glow for a planet seen from orbit. Draw it on a sphere ~2% larger than the planet.
// Brightness = Fresnel (long path through the limb) x sun-facing term, so only the lit limb glows and the glow
// fades across the terminator. Martian dust haze is butterscotch, with a hint of blue at the twilight edge.
Shader "EventHorizon/AtmosphereRim"
{
    Properties
    {
        [HDR] _DayColor ("Lit limb colour", Color) = (1.1, 0.72, 0.5, 1)
        [HDR] _TwilightColor ("Twilight colour", Color) = (0.35, 0.55, 1.0, 1)
        _Power ("Fresnel power", Range(1, 12)) = 4
        _Intensity ("Intensity", Range(0, 4)) = 1.2
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Rim"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _DayColor;
                float4 _TwilightColor;
                float _Power;
                float _Intensity;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 viewDir = normalize(GetCameraPositionWS() - i.positionWS);
                float3 sunDir = normalize(GetMainLight().direction);
                float fresnel = pow(1.0 - saturate(dot(n, viewDir)), _Power);
                float ndl = dot(n, sunDir);
                float lit = saturate(ndl * 1.5 + 0.35);
                float twilight = saturate(1.0 - abs(ndl) * 4.0);
                float3 col = lerp(_DayColor.rgb, _TwilightColor.rgb, twilight * 0.6) * fresnel * lit * _Intensity;
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
