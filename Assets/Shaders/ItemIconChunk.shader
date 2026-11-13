// A chunk of a terrain material for an inventory icon (GL612): the material's terrain texture laid on by the three
// axes (the lump has no UVs), times the vertex colour and a tint, lit as held items are. Only ever drawn by the
// editor's icon renderer, never in the world, so it has no shadow or depth passes.
Shader "Clube/ItemIconChunk"
{
    Properties
    {
        _MainTex ("Albedo", 2D) = "white" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Tiling ("Texture repeats across one unit", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float _Tiling;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
                float3 normalOS : TEXCOORD2;
                half4 color : COLOR;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionOS = input.positionOS.xyz;
                output.normalOS = input.normalOS;
                output.color = input.color * _Tint;
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float3 p = input.positionOS * _Tiling;
                float3 weight = pow(abs(normalize(input.normalOS)), 4.0);
                weight /= weight.x + weight.y + weight.z;
                half3 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, p.zy).rgb * weight.x
                    + SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, p.xz).rgb * weight.y
                    + SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, p.xy).rgb * weight.z;
                float3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight();
                half3 light = mainLight.color * (saturate(dot(normalWS, mainLight.direction)) * 0.8 + 0.2) + SampleSH(normalWS);
                return half4(albedo * input.color.rgb * light, 1.0);
            }
            ENDHLSL
        }
    }
}
