// Draws terrain in its materials (M11, M13, M14). Every vertex carries the three material
// ids of its triangle (UV channel 2) and how much of each it shows (UV channel 3), written
// by the mesher's material pass; the weights interpolate across the triangle, so hard
// seams (one id, weight 1) and blends use the same shader. Each material's texture is a
// layer of one texture array, indexed by id, and is projected from the three world axes
// (triplanar), so steep faces don't stretch and the mesh needs no UVs. With Debug colours
// on, each material is a flat colour from _MaterialColors instead (M15).
// MaterialRegistry sets _Textures and _MaterialColors before the material is used.
// _HardMaterialEdges (a global, set by the tool cursor, GL102): each point shows only the material it
// leans to most, so materials meet in a sharp edge halfway between samples rather than blending.
Shader "Clube/Terrain Materials"
{
    Properties
    {
        _Textures ("Textures (array, layer = material id)", 2DArray) = "" {}
        _TextureScale ("Texture repeats per metre", Float) = 0.5
        _TriplanarSharpness ("Triplanar sharpness", Range(1, 16)) = 4
        _Breakup ("Tiling breakup: noise cells per texture repeat", Range(0, 1)) = 0.3
        _TintVariation ("Large-scale brightness variation", Range(0, 0.5)) = 0.12
        [Toggle] _DebugColors ("Debug colours", Float) = 0
        // Coarse terrain LOD (P7c): set by TerrainLodView on its per-level copies; 0 (off) on the world's chunks.
        [HideInInspector] _LodClip ("LOD clip", Float) = 0
        [HideInInspector] _LodHole ("LOD hole", Vector) = (0, 0, 0, 0)
        [HideInInspector] _LodHoleMargin ("LOD margin", Float) = 0
        [HideInInspector] _LodCoverage ("LOD coverage", Vector) = (0, 0, 1, 0)
        [HideInInspector] _LodTint ("LOD tint", Color) = (1, 1, 1, 1)
        [HideInInspector] _LodCoverageMask ("LOD coverage mask", 2D) = "black" {}
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float _TextureScale;
            float _TriplanarSharpness;
            float _Breakup;
            float _TintVariation;
            float _DebugColors;
            float _LodClip;
            float4 _LodHole;
            float _LodHoleMargin;
            float4 _LodCoverage;
            float4 _LodTint;
        CBUFFER_END

        // Global (GL102), not per material.
        float _HardMaterialEdges;

        // Coarse terrain LOD (P7c, TerrainLodView), set on a copy of the material per level; 0 on the
        // world's own chunks. _LodClip 2: leave out the columns the world's chunks draw in
        // full (_LodCoverageMask, one texel per column; _LodCoverage: x, z of texel 0, column width,
        // size), except within _LodHoleMargin metres of a column they don't, so the level runs on a
        // little under their edge and no gap shows there. 3: that, and leave out _LodHole (x, z min,
        // x, z max in world metres), where the next finer level draws. _LodTint tints a level, to see the rings.
        TEXTURE2D(_LodCoverageMask);

        float LodCovered(float2 xz)
        {
            int2 texel = (int2)floor((xz - _LodCoverage.xy) / _LodCoverage.z);
            int size = (int)_LodCoverage.w;
            bool inside = all(texel >= 0) && all(texel < size);
            return inside ? LOAD_TEXTURE2D(_LodCoverageMask, texel).r : 0.0;
        }

        void ClipLod(float3 positionWS)
        {
            if (_LodClip < 0.5)
            {
                return;
            }
            float2 xz = positionWS.xz;
            if (_LodClip > 2.5)
            {
                clip(all(xz > _LodHole.xy) && all(xz < _LodHole.zw) ? -1.0 : 1.0);
            }
            float margin = _LodHoleMargin;
            float covered = LodCovered(xz) * LodCovered(xz + float2(margin, 0)) * LodCovered(xz - float2(margin, 0))
                          * LodCovered(xz + float2(0, margin)) * LodCovered(xz - float2(0, margin));
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
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D_ARRAY(_Textures);
            SAMPLER(sampler_Textures);

            // Set from the registry; not a material property, so it stays out of UnityPerMaterial.
            float4 _MaterialColors[64];

            // The layer each material id is drawn with (GL54): its own, or another's while it's hidden
            // (copper ore shown as stone). Set from the registry like the colours.
            float _LayerRemap[64];

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float3 materialIds : TEXCOORD2;
                float3 materialWeights : TEXCOORD3;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                // The same on all three vertices of a triangle, so no interpolation needed.
                nointerpolation float3 materialIds : TEXCOORD2;
                float3 materialWeights : TEXCOORD3;
                float fogFactor : TEXCOORD4;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.materialIds = input.materialIds;
                output.materialWeights = input.materialWeights;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            // Smooth value noise in 0-1 from a hash per lattice point; no texture needed.
            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // One projection with its tiling broken up (GL65): a second copy, turned and scaled,
            // shows through in noisy patches a few repeats across, so no grid of repeats lines up.
            half3 SampleBroken(float2 uv, float layer)
            {
                half3 first = SAMPLE_TEXTURE2D_ARRAY(_Textures, sampler_Textures, uv, layer).rgb;
                if (_Breakup <= 0.0)
                {
                    return first;
                }
                const float2x2 turn = float2x2(0.8, -0.6, 0.6, 0.8);
                half3 second = SAMPLE_TEXTURE2D_ARRAY(_Textures, sampler_Textures, mul(turn, uv) * 0.77 + 0.37, layer).rgb;
                float mixing = smoothstep(0.3, 0.7, ValueNoise(uv * _Breakup));
                return lerp(first, second, mixing);
            }

            // One material's texture projected along each world axis, mixed by how much the surface faces it.
            half3 Triplanar(float3 positionWS, float3 axisWeights, float layer)
            {
                float3 uv = positionWS * _TextureScale;
                half3 alongX = SampleBroken(uv.zy, layer);
                half3 alongY = SampleBroken(uv.xz, layer);
                half3 alongZ = SampleBroken(uv.xy, layer);
                return alongX * axisWeights.x + alongY * axisWeights.y + alongZ * axisWeights.z;
            }

            half3 Albedo(Varyings input, float3 normalWS)
            {
                float3 weights = input.materialWeights / max(dot(input.materialWeights, 1.0), 1e-5);
                if (_HardMaterialEdges > 0.5)
                {
                    // Only the strongest: an abrupt edge where two materials weigh the same.
                    weights = weights.x >= weights.y && weights.x >= weights.z ? float3(1, 0, 0)
                        : weights.y >= weights.z ? float3(0, 1, 0) : float3(0, 0, 1);
                }
                int3 ids = (int3)round(input.materialIds);
                ids = int3((int)_LayerRemap[ids.x], (int)_LayerRemap[ids.y], (int)_LayerRemap[ids.z]);
                half3 albedo = 0;
                if (_DebugColors > 0.5)
                {
                    albedo = _MaterialColors[ids.x].rgb * weights.x
                           + _MaterialColors[ids.y].rgb * weights.y
                           + _MaterialColors[ids.z].rgb * weights.z;
                }
                else
                {
                    float3 axisWeights = pow(abs(normalWS), _TriplanarSharpness);
                    axisWeights /= max(dot(axisWeights, 1.0), 1e-5);

                    // Skip materials that don't show here: most pixels need only one.
                    if (weights.x > 0.001)
                    {
                        albedo += Triplanar(input.positionWS, axisWeights, ids.x) * weights.x;
                    }
                    if (weights.y > 0.001)
                    {
                        albedo += Triplanar(input.positionWS, axisWeights, ids.y) * weights.y;
                    }
                    if (weights.z > 0.001)
                    {
                        albedo += Triplanar(input.positionWS, axisWeights, ids.z) * weights.z;
                    }
                    // Brighter and darker patches tens of metres across, so even the breakup doesn't read as a pattern.
                    albedo *= 1.0 + _TintVariation * (ValueNoise(input.positionWS.xz * 0.04) * 2.0 - 1.0);
                }
                return albedo;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                ClipLod(input.positionWS);
                float3 normalWS = normalize(input.normalWS);
                half3 albedo = Albedo(input, normalWS);
                if (_LodClip > 0.5)
                {
                    albedo *= _LodTint.rgb;
                }

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 direct = mainLight.color * saturate(dot(normalWS, mainLight.direction))
                             * mainLight.shadowAttenuation * mainLight.distanceAttenuation;
                half3 ambient = SampleSH(normalWS);

                half3 color = albedo * (direct + ambient);
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            float4 ShadowVertex(Attributes input) : SV_POSITION
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirection = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirection = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirection));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return positionCS;
            }

            half4 ShadowFragment() : SV_Target
            {
                return 0;
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

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            DepthVaryings DepthVertex(float4 positionOS : POSITION)
            {
                DepthVaryings output;
                output.positionWS = TransformObjectToWorld(positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half DepthFragment(DepthVaryings input) : SV_Target
            {
                ClipLod(input.positionWS);
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
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
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
                ClipLod(input.positionWS);
                float3 normalWS = normalize(input.normalWS);
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
