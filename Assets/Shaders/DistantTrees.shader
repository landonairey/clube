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
            float4 _FadeIn;
            float4 _FadeOut;
        CBUFFER_END

        #include "Assets/Shaders/Wind.hlsl"

        TEXTURE2D(_CoverageMask);

        // The crown sways in the wind with the voxel trees' leaves (Wind.hlsl): nothing below 0.45 of the height
        // (the meshes are one unit tall), up to 2% of the height at the top.
        float3 SwayCrown(float3 positionOS)
        {
            float3 positionWS = TransformObjectToWorld(positionOS);
            float height = length(float3(UNITY_MATRIX_M._m01, UNITY_MATRIX_M._m11, UNITY_MATRIX_M._m21));
            float crown = saturate((positionOS.y - 0.45) / 0.55);
            positionWS.xz += WindSway(positionWS, 0.02 * height * crown, 0.9);
            return positionWS;
        }

        // How much of a tree this level keeps at its distance (GL258), as a range of the dither: it shows where the
        // pixel's dither is at least x and under y. Fading in over _FadeIn (start, end metres) lowers x from 1 to 0; fading
        // out over _FadeOut lowers y from 1 to 0. Neighbouring levels fade over the same metres, so between them every
        // pixel shows exactly one.
        float2 LevelKeep(float3 root)
        {
            float d = distance(root.xz, _WorldSpaceCameraPos.xz);
            float into = saturate((d - _FadeIn.x) / max(_FadeIn.y - _FadeIn.x, 0.001));
            float outOf = saturate((d - _FadeOut.x) / max(_FadeOut.y - _FadeOut.x, 0.001));
            return float2(1.0 - into, 1.0 - outOf);
        }

        // A fragment of a tree fading between levels: dropped unless the screen's dither falls in the kept range.
        void ClipLevel(float4 positionCS, float2 keep)
        {
            float dither = frac(52.9829189 * frac(dot(positionCS.xy, float2(0.06711056, 0.00583715))));
            clip(dither >= keep.x && dither < keep.y ? 1.0 : -1.0);
        }

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
                float2 keep : TEXCOORD3;
            };

            Varyings Vertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                float3 root = TransformObjectToWorld(float3(0, 0, 0));
                float3 positionWS = SwayCrown(input.positionOS.xyz);
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
                output.keep = LevelKeep(root);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                ClipLevel(input.positionCS, input.keep);
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

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 keep : TEXCOORD0;
            };

            DepthVaryings DepthVertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                DepthVaryings output;
                float3 root = TransformObjectToWorld(float3(0, 0, 0));
                output.positionCS = Covered(root) ? float4(2, 2, 2, 1) : TransformWorldToHClip(SwayCrown(input.positionOS.xyz));
                output.keep = LevelKeep(root);
                return output;
            }

            half DepthFragment(DepthVaryings input) : SV_Target
            {
                ClipLevel(input.positionCS, input.keep);
                return 0;
            }
            ENDHLSL
        }

        // Depth and normals for the camera's depth texture: with SSAO on, URP builds it from this pass, and the clouds
        // (Clube/Clouds) read it to stop behind the trees; without it they drew over them.
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On

            HLSLPROGRAM
            #pragma vertex NormalsVertex
            #pragma fragment NormalsFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct NormalsVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float2 keep : TEXCOORD1;
            };

            NormalsVaryings NormalsVertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                NormalsVaryings output;
                float3 root = TransformObjectToWorld(float3(0, 0, 0));
                output.positionCS = Covered(root) ? float4(2, 2, 2, 1) : TransformWorldToHClip(SwayCrown(input.positionOS.xyz));
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.keep = LevelKeep(root);
                return output;
            }

            half4 NormalsFragment(NormalsVaryings input) : SV_Target
            {
                ClipLevel(input.positionCS, input.keep);
                float3 normalWS = SafeNormalize(input.normalWS);
            #if defined(_GBUFFER_NORMALS_OCT)
                float2 octahedral = PackNormalOctQuadEncode(normalWS);
                return half4(PackFloat2To888(saturate(octahedral * 0.5 + 0.5)), 0.0);
            #else
                return half4(normalWS, 0.0);
            #endif
            }
            ENDHLSL
        }
    }
}
