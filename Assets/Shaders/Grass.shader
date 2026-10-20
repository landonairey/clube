// Low-poly grass tufts (GL67), drawn instanced by GrassScatter. Each blade is a flat
// triangle in vertex colours, dark at the root and light at the tip; the colour's alpha is
// how far up the blade a vertex is, which also sets how far the wind sways it. Lit by the
// main light (with its shadows) and the ambient, both sides alike, with fog.
Shader "Clube/Grass"
{
    Properties
    {
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _WindStrength ("Wind sway (m at the tip)", Range(0, 0.3)) = 0.06
        _WindSpeed ("Wind speed", Range(0, 5)) = 1.3
        _FadeStart ("Shrinks from (m)", Float) = 20
        _FadeEnd ("Gone at (m)", Float) = 28
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float _WindStrength;
                float _WindSpeed;
                float _FadeStart;
                float _FadeEnd;
            CBUFFER_END

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
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                // Tufts shrink into the ground towards the edge of the grass, so it has no hard line.
                float3 rootWS = TransformObjectToWorld(float3(0, 0, 0));
                float fade = 1.0 - saturate((distance(rootWS, _WorldSpaceCameraPos) - _FadeStart) / max(_FadeEnd - _FadeStart, 0.01));
                positionWS = rootWS + (positionWS - rootWS) * fade;
                // The tips sway with a slow wave rolling across the ground.
                float phase = _Time.y * _WindSpeed + dot(positionWS.xz, float2(0.35, 0.21));
                float sway = (sin(phase) + 0.5 * sin(phase * 2.3 + 1.7)) * _WindStrength * input.color.a;
                positionWS.xz += float2(sway, sway * 0.6);
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color.rgb * _Tint.rgb;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                // Thin blades let light through: both faces are lit, the far one a little less.
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
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            Cull Off
            ColorMask R

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float _WindStrength;
                float _WindSpeed;
                float _FadeStart;
                float _FadeEnd;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 Vertex(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                // Tufts shrink into the ground towards the edge of the grass, so it has no hard line.
                float3 rootWS = TransformObjectToWorld(float3(0, 0, 0));
                float fade = 1.0 - saturate((distance(rootWS, _WorldSpaceCameraPos) - _FadeStart) / max(_FadeEnd - _FadeStart, 0.01));
                positionWS = rootWS + (positionWS - rootWS) * fade;
                float phase = _Time.y * _WindSpeed + dot(positionWS.xz, float2(0.35, 0.21));
                float sway = (sin(phase) + 0.5 * sin(phase * 2.3 + 1.7)) * _WindStrength * input.color.a;
                positionWS.xz += float2(sway, sway * 0.6);
                return TransformWorldToHClip(positionWS);
            }

            half Fragment() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
