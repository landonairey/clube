// The far terrain (P7, FarTerrainView): the world's heightfield out to tens of kilometres, drawn
// in flat colours by slope and height (grass, rock on steep ground, snow up high) with the main
// light, ambient light and the scene's fog, which gives distant ranges their haze. Over every
// chunk column the voxel world draws in full (_CoverageMask, one texel per column; _Coverage:
// world x and z of texel 0, the column width, the mask's size) every pass discards it, so the
// chunks' caves and edits show and nothing fights their depth; where chunks are still streaming
// in, it fills the gap. No shadows: the shadow map covers only the near chunks.
Shader "Clube/Far Terrain"
{
    Properties
    {
        _GrassColor ("Grass", Color) = (0.30, 0.40, 0.17, 1)
        _DryColor ("Dry grass (low, flat)", Color) = (0.45, 0.45, 0.24, 1)
        _RockColor ("Rock", Color) = (0.42, 0.40, 0.38, 1)
        _SnowColor ("Snow", Color) = (0.92, 0.93, 0.95, 1)
        _RockSlope ("Rock from slope (1 - normal.y)", Range(0, 1)) = 0.28
        _SnowHeight ("Snow from height (m)", Float) = 900
        _SnowBlend ("Snow blend (m)", Float) = 150
        // Set by FarTerrainView every frame.
        [HideInInspector] _CoverageMask ("Chunk coverage", 2D) = "black" {}
        [HideInInspector] _Coverage ("Coverage origin, column width, size", Vector) = (0, 0, 1, 0)
        [HideInInspector] _NearRect ("Near terrain rectangle", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _GrassColor;
            half4 _DryColor;
            half4 _RockColor;
            half4 _SnowColor;
            float _RockSlope;
            float _SnowHeight;
            float _SnowBlend;
            float4 _Coverage;
            float4 _NearRect;
        CBUFFER_END

        TEXTURE2D(_CoverageMask);

        // Per renderer (a property block): each level's debug tint.
        half4 _LevelTint;

        // Leaves out what nearer terrain draws: with _NearRect set (its z max above its z min), that
        // rectangle (x, z min, x, z max), the coarse voxel terrain's outer square (P7c); otherwise the
        // chunk columns in _CoverageMask.
        void ClipNear(float3 positionWS)
        {
            if (_NearRect.w > _NearRect.y)
            {
                clip(all(positionWS.xz > _NearRect.xy) && all(positionWS.xz < _NearRect.zw) ? -1.0 : 1.0);
                return;
            }
            int2 texel = (int2)floor((positionWS.xz - _Coverage.xy) / _Coverage.z);
            int size = (int)_Coverage.w;
            bool inside = all(texel >= 0) && all(texel < size);
            float covered = inside ? LOAD_TEXTURE2D(_CoverageMask, texel).r : 0.0;
            clip(covered > 0.5 ? -1.0 : 1.0);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float fogFactor : TEXCOORD2;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                ClipNear(input.positionWS);
                float3 normalWS = SafeNormalize(input.normalWS);
                float slope = 1.0 - normalWS.y;

                // Flat low land a little drier; steep ground rock; high gentle ground snow.
                half3 albedo = lerp(_GrassColor.rgb, _DryColor.rgb, 0.35 * saturate(1.0 - slope * 6.0));
                albedo = lerp(albedo, _RockColor.rgb, smoothstep(_RockSlope - 0.08, _RockSlope + 0.08, slope));
                float snow = smoothstep(_SnowHeight - _SnowBlend, _SnowHeight + _SnowBlend, input.positionWS.y)
                           * (1.0 - smoothstep(0.45, 0.65, slope));
                albedo = lerp(albedo, _SnowColor.rgb, snow);
                albedo *= _LevelTint.rgb;

                Light mainLight = GetMainLight();
                half3 direct = mainLight.color * saturate(dot(normalWS, mainLight.direction));
                half3 ambient = SampleSH(normalWS);
                half3 color = albedo * (direct + ambient);
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

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings DepthVertex(float4 positionOS : POSITION)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half DepthFragment(Varyings input) : SV_Target
            {
                ClipNear(input.positionWS);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On

            HLSLPROGRAM
            #pragma vertex NormalsVertex
            #pragma fragment NormalsFragment
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            Varyings NormalsVertex(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 NormalsFragment(Varyings input) : SV_Target
            {
                ClipNear(input.positionWS);
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
