// Draws terrain in its materials (M11, M13, M14). Every vertex carries the three material
// ids of its triangle (UV channel 2) and how much of each it shows (UV channel 3), written
// by the mesher's material pass; the weights interpolate across the triangle, so hard
// seams (one id, weight 1) and blends use the same shader. Each material's texture is a
// layer of one texture array, indexed by id, and is projected from the three world axes
// (triplanar), so steep faces don't stretch and the mesh needs no UVs. With Debug colours
// on, each material is a flat colour from _MaterialColors instead (M15).
// MaterialRegistry sets _Textures and _MaterialColors before the material is used.
Shader "Clube/Terrain Materials"
{
    Properties
    {
        _Textures ("Textures (array, layer = material id)", 2DArray) = "" {}
        _TextureScale ("Texture repeats per metre", Float) = 0.5
        _TriplanarSharpness ("Triplanar sharpness", Range(1, 16)) = 4
        [Toggle] _DebugColors ("Debug colours", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float _TextureScale;
            float _TriplanarSharpness;
            float _DebugColors;
        CBUFFER_END
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

            // One material's texture projected along each world axis, mixed by how much the surface faces it.
            half3 Triplanar(float3 positionWS, float3 axisWeights, float layer)
            {
                float3 uv = positionWS * _TextureScale;
                half3 alongX = SAMPLE_TEXTURE2D_ARRAY(_Textures, sampler_Textures, uv.zy, layer).rgb;
                half3 alongY = SAMPLE_TEXTURE2D_ARRAY(_Textures, sampler_Textures, uv.xz, layer).rgb;
                half3 alongZ = SAMPLE_TEXTURE2D_ARRAY(_Textures, sampler_Textures, uv.xy, layer).rgb;
                return alongX * axisWeights.x + alongY * axisWeights.y + alongZ * axisWeights.z;
            }

            half3 Albedo(Varyings input, float3 normalWS)
            {
                float3 weights = input.materialWeights / max(dot(input.materialWeights, 1.0), 1e-5);
                int3 ids = (int3)round(input.materialIds);
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
                }
                return albedo;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                half3 albedo = Albedo(input, normalWS);

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

            float4 DepthVertex(float4 positionOS : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(positionOS.xyz);
            }

            half DepthFragment() : SV_Target
            {
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
            };

            Varyings NormalsVertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 NormalsFragment(Varyings input) : SV_Target
            {
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
