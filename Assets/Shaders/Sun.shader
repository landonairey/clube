// The sun (GL277, SunDisc): a round ball of light on a camera-facing quad, a bright core with a soft glow round it, in
// the sun's colour (warm and deep at sunrise and sunset). Added onto the sky after it, without writing depth, so the
// ground and the clouds (opaque, GL277) hide it; where a cloud covers it, Clube/Low Poly Clouds lets its 12-sided outline
// shine through instead.
Shader "Clube/Sun"
{
    Properties
    {
        [HideInInspector] _SunColor ("Colour", Color) = (1, 0.95, 0.8, 1)
        [HideInInspector] _Strength ("Strength", Range(0, 2)) = 1
        _Core ("Core share of the quad", Range(0.05, 0.6)) = 0.22
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-420" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Sun"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _SunColor;
                float _Strength;
                float _Core;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 disc : TEXCOORD0;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.disc = input.uv * 2.0 - 1.0;
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float r = length(input.disc);
                float core = 1.0 - smoothstep(_Core * 0.85, _Core, r);
                float glow = pow(saturate(1.0 - r), 2.2) * 0.8;
                float3 colour = _SunColor.rgb * (core * 2.2 + glow) * _Strength;
                return half4(colour, 0.0);
            }
            ENDHLSL
        }
    }
}
