// The outline round what the player can use (GL368): the object's back faces pushed out along their normals by
// _Width metres and drawn flat in _Color behind it, so only a thin rim shows round its silhouette. Things in front still
// hide it (it tests depth, but doesn't write it).
Shader "Clube/Interact Outline"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 1, 1, 0.9)
        _Width ("Width (m)", Float) = 0.025
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+50" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Front

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Width;
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
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = normalize(TransformObjectToWorldNormal(input.normalOS));
                output.positionCS = TransformWorldToHClip(positionWS + normalWS * _Width);
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
