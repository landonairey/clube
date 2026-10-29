// Trees far off (GL162, DistantTreeView): low-poly meshes drawn instanced, coloured by their
// vertex colours (bark, leaves), lit by the main light and ambient light, in the scene's fog.
// A tree whose root stands on a chunk column the voxel world draws in full (_CoverageMask, one
// texel per column; _Coverage: x, z of texel 0, column width, size) is dropped in the vertex
// stage: the voxel world grows its own trees there. Each tree is a little brighter or darker,
// hashed from where it stands.
Shader "Clube/Distant Trees"
{
    Properties
    {
        [HideInInspector] _CoverageMask ("Chunk coverage", 2D) = "black" {}
        [HideInInspector] _Coverage ("Coverage origin, column width, size", Vector) = (0, 0, 1, 0)
        _Variation ("Brightness variation", Range(0, 0.5)) = 0.15
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _Coverage;
            float _Variation;
        CBUFFER_END

        TEXTURE2D(_CoverageMask);

        // True when the voxel world draws the column under this root itself.
        bool Covered(float3 root)
        {
            int2 texel = (int2)floor((root.xz - _Coverage.xy) / _Coverage.z);
            int size = (int)_Coverage.w;
            bool inside = all(texel >= 0) && all(texel < size);
            return inside && LOAD_TEXTURE2D_LOD(_CoverageMask, texel, 0).r > 0.5;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                half3 color : TEXCOORD1;
                float fogFactor : TEXCOORD2;
            };

            Varyings Vertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                float3 root = TransformObjectToWorld(float3(0, 0, 0));
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                if (Covered(root))
                {
                    // Outside the clip volume on every vertex: the triangle is dropped.
                    output.positionCS = float4(2, 2, 2, 1);
                }
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                float shade = 1.0 + _Variation * (frac(sin(dot(root.xz, float2(12.9898, 78.233))) * 43758.5453) * 2.0 - 1.0);
                output.color = input.color.rgb * shade;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float3 normalWS = SafeNormalize(input.normalWS);
                Light mainLight = GetMainLight();
                half3 direct = mainLight.color * saturate(dot(normalWS, mainLight.direction));
                half3 ambient = SampleSH(normalWS);
                half3 color = input.color * (direct + ambient);
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVertex
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 DepthVertex(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 root = TransformObjectToWorld(float3(0, 0, 0));
                return Covered(root) ? float4(2, 2, 2, 1) : TransformObjectToHClip(input.positionOS.xyz);
            }

            half DepthFragment() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
