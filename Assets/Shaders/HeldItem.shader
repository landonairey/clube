// The tool or hand held in front of the camera (GL269, HeldToolView), and the same models lying dropped in the world
// (GL610, DroppedItems): flat low-poly parts coloured by their vertex colours, lit by the main light (with its shadows)
// and the sky's ambient light. The mesh's first UV says what each part is made of (GL609, LowPolyMeshBuilder.Surface):
// wood gets a grain along the handle, metal a glint off its facets and a slow gleam passing over it, by how much it
// shines. Writes depth and normals too, so the clouds (which stop at the camera's depth texture, GL267) stay behind it.
Shader "Clube/Held Item"
{
    Properties
    {
        _Tint ("Tint", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _Tint;
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
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/Shaders/AdditionalLights.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                float2 surface : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                half4 color : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float4 positionOSAndFog : TEXCOORD3;
                nointerpolation float2 surface : TEXCOORD4;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = half4(input.color.rgb * _Tint.rgb, input.color.a);
                output.positionOSAndFog = float4(input.positionOS.xyz, ComputeFogFactor(output.positionCS.z));
                output.surface = input.surface;
                return output;
            }

            // Wood grain (GL609): dark and pale streaks running along the handle (the model's Y), wandering a little.
            half3 Grain(half3 wood, float3 p)
            {
                float across = (p.x * 1.3 + p.z) * 150.0 + sin(p.y * 31.0 + p.x * 40.0) * 0.45 + sin(p.y * 9.0) * 0.6;
                float streak = frac(across);
                half dark = step(0.7, streak);
                half pale = step(streak, 0.12);
                return wood * (1.0 - 0.24 * dark + 0.14 * pale);
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                // A torch's flame (vertex alpha 0, GL294) glows by itself, whatever the light.
                if (input.color.a < 0.5)
                {
                    return half4(input.color.rgb * 1.8, 1.0);
                }
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half shade = mainLight.shadowAttenuation;
                half3 direct = mainLight.color * (saturate(dot(normalWS, mainLight.direction)) * shade * 0.8 + 0.2);
                half3 ambient = SampleSH(normalWS);
                direct += AdditionalDiffuse(input.positionWS, normalWS, input.positionCS);
                half3 albedo = input.color.rgb;
                float3 positionOS = input.positionOSAndFog.xyz;
                half3 gleam = 0;
                if (input.surface.x > 1.5)
                {
                    // Metal (GL609): each flat facet glints as it lines up with the light, and a soft gleam passes along
                    // the tool now and then, as the facets of the water shimmer.
                    half shine = input.surface.y;
                    float3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                    half glint = pow(saturate(dot(reflect(-mainLight.direction, normalWS), viewWS)), 18.0);
                    float sweep = frac(dot(positionOS, float3(0.6, 1.1, 1.7)) * 1.6 - _Time.y * 0.22);
                    half band = smoothstep(0.0, 0.06, sweep) * (1.0 - smoothstep(0.06, 0.16, sweep));
                    gleam = shine * (mainLight.color * glint * shade * 0.9 + (mainLight.color * (0.3 + 0.7 * shade) + ambient) * band * 0.16);
                    albedo *= 1.0 + 0.1 * shine;
                }
                else if (input.surface.x > 0.5)
                {
                    albedo = Grain(albedo, positionOS);
                }
                half3 color = albedo * (direct + ambient) + gleam;
                return half4(MixFog(color, input.positionOSAndFog.w), 1.0);
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
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            float4 Vertex(float4 positionOS : POSITION, float3 normalOS : NORMAL) : SV_POSITION
            {
                float3 positionWS = TransformObjectToWorld(positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(normalOS);
            #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
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

            half4 Fragment() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ColorMask R

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            float4 Vertex(float4 positionOS : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(positionOS.xyz);
            }

            half Fragment() : SV_Target
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
            #pragma vertex Vertex
            #pragma fragment Fragment
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

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
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
