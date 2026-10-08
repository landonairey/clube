// The tool cursor (GL4, GL24): the surface mesh's triangles inside the voxels a tool
// reaches, shaded and outlined in their vertex colours. Each point is pulled 2 cm towards
// the camera so the faces and lines draw over the surface they lie on instead of fighting
// it for depth (a polygon Offset barely moves lines); terrain in front of them still hides
// them.
Shader "Clube/Tool Cursor Lines"
{
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+100" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Lines"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                positionWS += normalize(GetCameraPositionWS() - positionWS) * 0.02;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.color = input.color;
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                return input.color;
            }
            ENDHLSL
        }
    }
}
