// Low-poly clouds (GL277, CloudLayer): faceted puffs drawn instanced, flat shaded like the low-poly tree tops. Lit by the
// light DayNightCycle gives the clouds (_CloudLightDir, _CloudLightColor: the sun, in the sunrise's or sunset's colour, a
// while after it leaves the land; the moon at night), wrapped so the shade side isn't black, the underside a little
// darker, silver edges when the light is behind them, and the sky's ambient light. Where a cloud stands in front of the
// sun (_SunShineDir, _SunShineColor, _SunShineRadius, set by SunDisc) the sun's 12-sided outline shines through it.
// In the scene's fog, so the far ones fade into the haze.
Shader "Clube/Low Poly Clouds"
{
    Properties
    {
        _Color ("Colour", Color) = (0.97, 0.97, 1, 1)
        _Underside ("Underside shade", Range(0, 1)) = 0.8
        _Ambient ("Sky light", Range(0, 2)) = 1.1
        _Silver ("Silver lining", Range(0, 2)) = 0.7
        _ShineThrough ("Sun through cloud", Range(0, 3)) = 1.4
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
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Underside;
                float _Ambient;
                float _Silver;
                float _ShineThrough;
            CBUFFER_END

            float4 _CloudLightDir;
            float4 _CloudLightColor;
            float4 _SunShineDir;
            float4 _SunShineColor;
            float _SunShineRadius;

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
                half height : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

            Varyings Vertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.height = input.color.r;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            // How far inside a 12-sided polygon of radius 1 a point is, and how close to its edge (1 on the edge).
            float2 Polygon(float2 p)
            {
                const float Sector = 6.2831853 / 12.0;
                float r = length(p);
                float angle = atan2(p.y, p.x) + 3.1415927;
                float across = fmod(angle, Sector) - Sector * 0.5;
                float edge = cos(Sector * 0.5) / cos(across);
                float inside = 1.0 - smoothstep(edge - 0.04, edge, r);
                float ring = 1.0 - smoothstep(0.0, 0.09, abs(r - edge));
                return float2(inside, ring);
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                bool given = _CloudLightColor.a > 0.5;
                Light main = GetMainLight();
                float3 towardsLight = given ? normalize(_CloudLightDir.xyz) : main.direction;
                half3 light = given ? _CloudLightColor.rgb : main.color;
                float3 view = normalize(input.positionWS - GetCameraPositionWS());

                // Lit by the light and the sky alone, with no floor of its own, so clouds go as dark as the night (GL284).
                float wrap = saturate(dot(normalWS, towardsLight) * 0.5 + 0.5);
                float direct = saturate(dot(normalWS, towardsLight));
                half3 ambient = max(SampleSH(normalWS), 0.0) * _Ambient * (0.75 + 0.25 * normalWS.y);
                half shade = lerp(_Underside, 1.0, input.height);
                // Each facet a touch lighter or darker by which way it faces, so the polygon edges read in any light,
                // moonlight included, where the faces would otherwise all come out one grey.
                float facet = 0.84 + 0.08 * (sin(dot(normalWS, float3(5.1, 9.7, 3.3))) + 1.0);
                // A floor that scales with the light itself (white by day, nothing at night) keeps the shade side from going muddy.
                half3 colour = _Color.rgb * (ambient * 1.05 + light * (0.35 + wrap * 0.5 + direct * 0.55)) * shade * facet;

                // Silver edges: faces turned away from the camera, with the light behind the cloud.
                float behind = pow(saturate(dot(view, towardsLight)), 6.0);
                float rim = pow(1.0 - saturate(dot(normalWS, -view)), 2.0);
                colour += light * behind * rim * _Silver;

                // The sun behind the cloud: its 12-sided outline, and a little of its light, shine through.
                if (_SunShineColor.a > 0.0)
                {
                    float3 sun = normalize(_SunShineDir.xyz);
                    float facing = dot(view, sun);
                    if (facing > 0.9)
                    {
                        float3 right = normalize(cross(abs(sun.y) < 0.99 ? float3(0, 1, 0) : float3(1, 0, 0), sun));
                        float3 up = cross(sun, right);
                        float2 p = float2(dot(view, right), dot(view, up)) / max(_SunShineRadius, 1e-4);
                        float2 polygon = Polygon(p);
                        colour += _SunShineColor.rgb * _SunShineColor.a * (polygon.y * 1.2 + polygon.x * 0.35) * _ShineThrough;
                    }
                }

                colour = MixFog(colour, input.fogFactor);
                return half4(colour, 1.0);
            }
            ENDHLSL
        }
    }
}
