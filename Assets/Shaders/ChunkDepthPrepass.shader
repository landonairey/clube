// Writes a mesh into the depth buffer only (no colour), after the opaque objects
// and before the transparent ones. Added as a second material on a transparent
// chunk, it lets the chunk hide its own back surfaces: URP's transparent Lit
// material doesn't write depth, so without this the triangles of one mesh draw in
// buffer order and inner walls can show through outer ones.
Shader "Clube/Chunk Depth Prepass"
{
    SubShader
    {
        // Geometry+450: after ordinary opaques (so things behind the chunk are already
        // drawn and still blend through it), before the Transparent queue at 3000.
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry+450" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "DepthPrepass"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
