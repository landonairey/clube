// A soft glow for the tool cursor (GL4), drawn additively on the terrain surface: each
// glow sits on a polygon corner, so the half above the ground shows and terrain in front
// of it still hides it.
// Brightest where the sphere faces the camera and fading to nothing at its rim, so it
// reads as a glow rather than a ball. The colour comes from _BaseColor (alpha scales it),
// set per glow through a MaterialPropertyBlock.
Shader "Clube/Tool Cursor Glow"
{
    Properties
    {
        _BaseColor ("Colour", Color) = (1, 0.95, 0.7, 0.5)
        _Falloff ("Edge falloff", Range(0.5, 8)) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+100" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Glow"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Falloff;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewWS : TEXCOORD1;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewWS = GetWorldSpaceViewDir(positionWS);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                half facing = saturate(dot(normalize(input.normalWS), normalize(input.viewWS)));
                half glow = pow(facing, _Falloff) * _BaseColor.a;
                return half4(_BaseColor.rgb * glow, 0);
            }
            ENDHLSL
        }
    }
}
