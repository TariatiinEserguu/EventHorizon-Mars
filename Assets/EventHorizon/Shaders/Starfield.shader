// Point stars drawn as one mesh: every star is a 4-vertex quad that the vertex shader expands to a fixed size
// in screen pixels, so stars stay crisp at any resolution and distance. Brightness, size and blackbody colour
// come from the mesh (built by Starfield.cs from a realistic magnitude distribution). No twinkling: in vacuum
// starlight doesn't scintillate. An additive blend lets bright stars reach HDR and bloom slightly.
Shader "EventHorizon/Starfield"
{
    Properties
    {
        _Intensity ("Intensity", Range(0, 4)) = 1
        _SizeScale ("Size scale (pixels)", Range(0.5, 4)) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent-450" "RenderType" = "Transparent" "IgnoreProjector" = "True" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Stars"
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
                float _Intensity, _SizeScale;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;        // rgb = colour, a = brightness (0..1)
                float2 corner : TEXCOORD0;   // -1..1
                float2 size : TEXCOORD1;     // x = size in pixels
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 corner : TEXCOORD0;
                float4 color : TEXCOORD1;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float4 clip = TransformObjectToHClip(v.positionOS.xyz);
                float2 px = v.corner * v.size.x * _SizeScale;
                clip.xy += px * 2.0 / _ScreenParams.xy * clip.w;
                o.positionCS = clip;
                o.corner = v.corner;
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float r2 = dot(i.corner, i.corner);
                float psf = exp(-r2 * 5.0);          // Gaussian point-spread function
                float core = exp(-r2 * 28.0) * 0.6;  // tight core so bright stars read as points
                float b = i.color.a;
                float3 c = i.color.rgb * (psf + core) * (b * b * 3.5 + b * 0.25) * _Intensity;
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
}
