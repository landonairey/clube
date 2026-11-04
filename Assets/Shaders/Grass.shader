// Low-poly flora drawn instanced: grass tufts and wheat (GL67, GrassScatter) and bushes (BushScatter).
// Every part is flat triangles in vertex colours, dark at the root and light at the tip; the colour's
// alpha is how far up a vertex is, which also sets how far the wind sways it (Wind.hlsl). Lit by the
// main light (with its shadows) and the ambient, both sides alike, with fog.
// Grass turns from green to straw as it grows taller: the instance's height (its scale) blends the
// vertex colour towards _StrawColor between _StrawFrom and _StrawAt metres, by _StrawStrength
// (0 for bushes). Taller instances sway further, in proportion to _SwayHeight.
Shader "Clube/Grass"
{
    Properties
    {
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _WindStrength ("Wind sway (m at the tip, at the reference height)", Range(0, 0.3)) = 0.06
        _WindSpeed ("Wind speed", Range(0, 5)) = 1.3
        _SwayHeight ("Reference height for the sway (m)", Float) = 0.3
        _FadeStart ("Shrinks from (m)", Float) = 20
        _FadeEnd ("Gone at (m)", Float) = 28
        _ThinStart ("Thins out from (m)", Float) = 100000
        _ThinMin ("Share left where it starts to shrink", Range(0.02, 1)) = 1
        _LawnBlend ("Takes the far grass colour with distance", Range(0, 1)) = 0
        _StrawColor ("Straw colour", Color) = (0.78, 0.66, 0.36, 1)
        _StrawFrom ("Green up to (m tall)", Float) = 0.3
        _StrawAt ("Straw from (m tall)", Float) = 0.8
        _StrawStrength ("Straw strength", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Assets/Shaders/Wind.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _Tint;
            float _WindStrength;
            float _WindSpeed;
            float _SwayHeight;
            float _FadeStart;
            float _FadeEnd;
            float _ThinStart;
            float _ThinMin;
            float _LawnBlend;
            half4 _StrawColor;
            float _StrawFrom;
            float _StrawAt;
            float _StrawStrength;
        CBUFFER_END

        // The instance's height in metres: the length of its up axis (the meshes are 1 m tall before scaling).
        float InstanceHeight()
        {
            return length(float3(UNITY_MATRIX_M._m01, UNITY_MATRIX_M._m11, UNITY_MATRIX_M._m21));
        }

        // The grass's colour seen from afar (GL257), globals set by GrassScatter, shared with the terrain shaders.
        #include "Assets/Shaders/GrassLawn.hlsl"

        // How far towards the far grass colour an instance at this distance has turned, 0-1.
        float LawnShare(float distanceToCamera)
        {
            return saturate((distanceToCamera - _ThinStart) / max(_FadeEnd - _ThinStart, 0.01)) * _LawnBlend;
        }

        // Where a vertex stands: past _ThinStart GrassScatter sends fewer instances (GL379), so those left are wider to cover the same
        // ground (GL257); towards the edge of the drawn area they shrink into the ground, so there's no hard
        // line; and the wind sways the tip.
        float3 FloraPosition(float3 positionOS, float tip)
        {
            float3 positionWS = TransformObjectToWorld(positionOS);
            float3 rootWS = TransformObjectToWorld(float3(0, 0, 0));
            float distanceToCamera = distance(rootWS, _WorldSpaceCameraPos);
            float keep = lerp(1.0, _ThinMin, saturate((distanceToCamera - _ThinStart) / max(_FadeStart - _ThinStart, 0.01)));
            float fade = 1.0 - saturate((distanceToCamera - _FadeStart) / max(_FadeEnd - _FadeStart, 0.01));
            float3 offset = (positionWS - rootWS) * fade;
            // Survivors stand taller as well as wider, so far grass still reads as grass (GL378).
            float widen = min(rsqrt(max(keep, 0.01)), 2.5);
            offset.xz *= widen;
            offset.y *= lerp(1.0, widen, 0.5);
            positionWS = rootWS + offset;
            float reach = clamp(InstanceHeight() / max(_SwayHeight, 0.01), 0.5, 4.0);
            positionWS.xz += WindSway(positionWS, _WindStrength * reach * tip, _WindSpeed);
            return positionWS;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/Shaders/AdditionalLights.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half3 color : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

            Varyings Vertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                float3 positionWS = FloraPosition(input.positionOS.xyz, input.color.a);
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                // Tall grass dries to straw, darker at the root like the green.
                float straw = saturate((InstanceHeight() - _StrawFrom) / max(_StrawAt - _StrawFrom, 0.01)) * _StrawStrength;
                half3 dry = _StrawColor.rgb * lerp(0.45, 1.0, input.color.a);
                output.color = lerp(input.color.rgb, dry, straw) * _Tint.rgb;
                // Far off, grass takes the colour the ground under it shows from afar, so the two meet without a line.
                float3 rootWS = TransformObjectToWorld(float3(0, 0, 0));
                float lawnShare = LawnShare(distance(rootWS, _WorldSpaceCameraPos));
                if (lawnShare > 0.0)
                {
                    half3 lawn = GrassLawnAt(rootWS.xz) * lerp(0.75, 1.15, input.color.a);
                    output.color = lerp(output.color, lawn, lawnShare);
                }
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                // Thin blades and leaves let light through: both faces are lit, the far one a little less.
                half facing = abs(dot(normalWS, mainLight.direction)) * 0.7 + 0.3;
                half3 direct = mainLight.color * facing * mainLight.shadowAttenuation * mainLight.distanceAttenuation;
                // Torches light the grass too (GL294).
                direct += AdditionalDiffuse(input.positionWS, normalWS, input.positionCS);
                half3 ambient = SampleSH(half3(0, 1, 0));
                half3 color = input.color * (direct + ambient);
                color = MixFog(color, input.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            Cull Off
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 ShadowVertex(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = FloraPosition(input.positionOS.xyz, input.color.a);
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
            Cull Off
            ColorMask R

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 Vertex(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return TransformWorldToHClip(FloraPosition(input.positionOS.xyz, input.color.a));
            }

            half Fragment() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // Depth and normals for the camera's depth texture: with SSAO on, URP builds it from this pass, and the clouds
        // (Clube/Clouds) read it to stop behind what's in front of them; without it they drew over the grass and bushes.
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            Cull Off
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
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            Varyings NormalsVertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                output.positionCS = TransformWorldToHClip(FloraPosition(input.positionOS.xyz, input.color.a));
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
