// The outline round what the player can use (GL368, GL372): a pixel-thin line round the object's silhouette. It takes two
// materials of this shader, drawn in order by their render queues:
// - the mask (_WidthPixels 0, ColorMask 0): marks the object's whole footprint on screen in a stencil bit, through
//   anything in front of it;
// - the outline: the object again, each vertex pushed out _WidthPixels on screen along its smoothed normal, drawn only where
//   the mask isn't, so only a rim just outside the silhouette shows, never lines across the object itself.
// Things in front still hide the outline (it tests depth, but doesn't write it).
Shader "Clube/Interact Outline"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 1, 1, 0.95)
        _WidthPixels ("Width (pixels)", Float) = 1.5
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil compare", Float) = 6
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilPass ("Stencil pass", Float) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Depth test", Float) = 4
        _ColorMask ("Colour mask", Float) = 15
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+51" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest [_ZTest]
            Cull Off
            ColorMask [_ColorMask]

            Stencil
            {
                Ref 64
                ReadMask 64
                WriteMask 64
                Comp [_StencilComp]
                Pass [_StencilPass]
            }

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _WidthPixels;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                float4 positionCS = TransformObjectToHClip(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float2 normalCS = mul((float3x3)GetWorldToHClipMatrix(), normalWS).xy;
                float length2 = dot(normalCS, normalCS);
                if (_WidthPixels > 0 && length2 > 1e-8)
                {
                    // Pixels to clip space: 2 / screen size, times w so the width holds at any distance.
                    positionCS.xy += normalCS * rsqrt(length2) * _WidthPixels * 2.0 / _ScreenParams.xy * positionCS.w;
                }
                output.positionCS = positionCS;
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                return _Color;
            }
            ENDHLSL
        }
    }
}
