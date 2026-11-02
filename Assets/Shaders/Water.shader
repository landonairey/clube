// Rivers and lakes (GL309, WaterView): a low-poly water surface of flat triangles that shimmer between blue, light blue
// and now and then white. On rivers the light bands run down the river along its flow (UV0), so the water shows which way
// it runs; on ponds and lakes triangles twinkle at random. Corners bob a little by their position, so the facets tilt and
// catch the light. Each triangle carries its centre (UV1) and kind and a random number (UV2), so it changes colour whole.
Shader "Clube/Water"
{
    Properties
    {
        _Deep ("Deep", Color) = (0.11, 0.30, 0.52, 1)
        _Light ("Light", Color) = (0.36, 0.63, 0.86, 1)
        _Foam ("Foam", Color) = (0.93, 0.97, 1, 1)
        _Alpha ("Alpha", Range(0, 1)) = 0.8
        _FlowBand ("River band length (m)", Float) = 2.6
        _FlowSpeed ("River band speed", Float) = 1.4
        _Bob ("Bob height (m)", Float) = 0.035
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-50" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_fog
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/Shaders/AdditionalLights.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Deep;
                half4 _Light;
                half4 _Foam;
                half _Alpha;
                float _FlowBand;
                float _FlowSpeed;
                float _Bob;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 flow : TEXCOORD0;
                float2 centre : TEXCOORD1;
                float2 kind : TEXCOORD2;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                nointerpolation half3 color : TEXCOORD1;
                float fog : TEXCOORD2;
            };

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            // The triangle's colour: a level 0-1 from deep to light, and foam on top of that.
            half3 Shimmer(float2 flow, float2 centre, float river, float random)
            {
                float t = _Time.y;
                float level;
                float foam;
                float speed = length(flow);
                if (river > 0.5 && speed > 0.001)
                {
                    // Bands down the river: the phase falls along the flow, so the light moves downstream.
                    float2 along = flow / speed;
                    float phase = dot(centre, along) / _FlowBand - t * _FlowSpeed * (0.6 + speed) + random * 0.35;
                    float wave = frac(phase);
                    level = smoothstep(0.55, 0.85, wave) * (1.0 - smoothstep(0.9, 1.0, wave));
                    foam = step(0.86, wave) * step(1.0 - speed * 0.35, random);
                }
                else
                {
                    // Lakes: each triangle twinkles on its own beat.
                    float beat = frac(t * (0.12 + random * 0.2) + random * 7.0);
                    level = smoothstep(0.78, 0.9, beat) * (1.0 - smoothstep(0.92, 1.0, beat));
                    foam = step(0.97, beat) * step(0.8, random);
                }
                half3 color = lerp(_Deep.rgb, _Light.rgb, level * (0.75 + 0.25 * random));
                return lerp(color, _Foam.rgb, foam);
            }

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                // Corners bob by where they are, so triangles sharing one move together and the facets tilt.
                float seed = Hash(floor(positionWS.xz * 4.0 + 0.5));
                positionWS.y += sin(_Time.y * (1.1 + seed) + seed * 6.2831) * _Bob;
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.color = Shimmer(input.flow, input.centre, input.kind.x < 1.5 ? 1.0 : 0.0, input.kind.y);
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                // Flat facets: the normal of the triangle under the pixel.
                float3 normalWS = normalize(cross(ddy(input.positionWS), ddx(input.positionWS)));
                if (normalWS.y < 0)
                {
                    normalWS = -normalWS;
                }
                Light mainLight = GetMainLight();
                half3 direct = mainLight.color * (saturate(dot(normalWS, mainLight.direction)) * 0.6 + 0.4);
                half3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
                half glint = pow(saturate(dot(reflect(-mainLight.direction, normalWS), viewDir)), 48.0);
                half3 ambient = SampleSH(half3(0, 1, 0));
                direct += AdditionalDiffuse(input.positionWS, normalWS, input.positionCS);
                half3 color = input.color * (direct + ambient) + mainLight.color * glint * 0.6;
                color = MixFog(color, input.fog);
                return half4(color, _Alpha);
            }
            ENDHLSL
        }
    }
}
