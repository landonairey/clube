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

        // Where a vertex stands: shrunk into the ground towards the edge of the drawn area, so it has no
        // hard line, and swayed by the wind at its tip.
        float3 FloraPosition(float3 positionOS, float tip)
        {
            float3 positionWS = TransformObjectToWorld(positionOS);
            float3 rootWS = TransformObjectToWorld(float3(0, 0, 0));
            float fade = 1.0 - saturate((distance(rootWS, _WorldSpaceCameraPos) - _FadeStart) / max(_FadeEnd - _FadeStart, 0.01));
            positionWS = rootWS + (positionWS - rootWS) * fade;
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
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

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
    }
}
