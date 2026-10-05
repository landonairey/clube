// Draws a world-space build grid on a mesh: lines where the surface crosses each grid
// cell's x and z boundaries, so the grid drapes over the terrain like a map grid (no
// height contours), a constant number of pixels wide. Added as an extra material on
// terrain chunks; the lab's BuildGridOverlay turns it on and sets the cell size and colour
// through global shader values, so with the grid off (opacity 0, the default) it draws nothing.
Shader "Clube/Build Grid Overlay"
{
    SubShader
    {
        // After the terrain, opaque or transparent, so the lines sit on top of it.
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+10" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "BuildGrid"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            // Pulls the lines a hair towards the camera so they don't fight the surface.
            Offset -1, -1
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // Globals, set by BuildGridOverlay (Clube.Debug).
            float _ClubeBuildGridOpacity;
            float _ClubeBuildGridCellSize;
            float _ClubeBuildGridLineWidth;
            float4 _ClubeBuildGridColor;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                if (_ClubeBuildGridOpacity <= 0.0)
                {
                    discard;
                }

                // Distance to the nearest cell boundary on each axis, in pixels.
                float2 cell = input.positionWS.xz / max(_ClubeBuildGridCellSize, 0.001);
                float2 toLine = abs(frac(cell - 0.5) - 0.5) / max(fwidth(cell), 1e-5);
                float nearest = min(toLine.x, toLine.y);

                // Anti-aliased line of the requested width.
                float coverage = 1.0 - saturate(nearest - (_ClubeBuildGridLineWidth * 0.5 - 0.5));
                if (coverage <= 0.0)
                {
                    discard;
                }
                return half4(_ClubeBuildGridColor.rgb, coverage * _ClubeBuildGridOpacity);
            }
            ENDHLSL
        }
    }
}
