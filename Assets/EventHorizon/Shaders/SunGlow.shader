// Camera-facing halo around the Sun (corona and scattered glare). Kept subtle so details near the Sun stay
// readable. The quad is billboarded in the vertex shader, so it needs no script.
Shader "EventHorizon/SunGlow"
{
    Properties
    {
        [HDR] _Color ("Glow colour", Color) = (1.4, 1.1, 0.75, 1)
        _Falloff ("Falloff", Range(1, 12)) = 4.5
        _Intensity ("Intensity", Range(0, 3)) = 0.6
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+10" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Falloff, _Intensity;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 center = TransformObjectToWorld(float3(0, 0, 0));
                float scale = length(float3(UNITY_MATRIX_M._m00, UNITY_MATRIX_M._m10, UNITY_MATRIX_M._m20));
                float3 right = UNITY_MATRIX_V._m00_m01_m02;
                float3 up = UNITY_MATRIX_V._m10_m11_m12;
                float3 world = center + (right * v.positionOS.x + up * v.positionOS.y) * scale;
                o.positionCS = TransformWorldToHClip(world);
                o.uv = v.uv * 2.0 - 1.0;
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float r = length(i.uv);
                float g = exp(-r * _Falloff) * saturate(1.0 - r);
                return half4(_Color.rgb * g * _Intensity, 1);
            }
            ENDHLSL
        }
    }
}
