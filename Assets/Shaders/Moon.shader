// The moon (GL248, MoonDisc), low poly (GL278, GL285): a 12-sided disc whose phase edge is a six-segment polygon line,
// placed where a ball lit from _SunDirection would have it, so the phase is whatever
// the angle between the moon and the sun makes it: full opposite the sun, a thin crescent beside it. Darker seas are
// hashed across its face. The unlit side shows faintly at night (earthshine) and not at all by day, when the lit side
// is pale. Drawn after the sky without writing depth, so the ground in front hides it.
Shader "Clube/Moon"
{
    Properties
    {
        _Color ("Lit colour", Color) = (1, 0.97, 0.9, 1)
        _DayAlpha ("Opacity by day", Range(0, 1)) = 0.55
        _Earthshine ("Earthshine at night", Range(0, 0.3)) = 0.06
        [HideInInspector] _SunDirection ("Towards the sun", Vector) = (0, 1, 0, 0)
        [HideInInspector] _Daylight ("Daylight", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-400" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Moon"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _DayAlpha;
                float _Earthshine;
                float4 _SunDirection;
                float _Daylight;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 disc : TEXCOORD0;
                float3 right : TEXCOORD1;
                float3 up : TEXCOORD2;
                float3 towardsViewer : TEXCOORD3;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.disc = input.uv * 2.0 - 1.0;
                output.right = normalize(UNITY_MATRIX_M._m00_m10_m20);
                output.up = normalize(UNITY_MATRIX_M._m01_m11_m21);
                // The disc faces along its forward axis away from the camera.
                output.towardsViewer = -normalize(UNITY_MATRIX_M._m02_m12_m22);
                return output;
            }

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            // Smooth value noise over the face, for the seas.
            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash(i), b = Hash(i + float2(1, 0)), c = Hash(i + float2(0, 1)), d = Hash(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                // Low poly (GL278, GL285): the outline a 12-sided polygon and the phase's edge a polygon line of six segments
                // through the same heights, both turned with the light. On a ball the edge of the light is an ellipse,
                // u = -k sqrt(1 - v^2) across the disc (u towards the sun, k how far the sun is behind the viewer); here the
                // circle's half-width is the 12-gon's, so the edge is the outline squashed: straight segments, clean corners.
                const float Step = 3.1415927 / 6.0;
                float3 sun = normalize(_SunDirection.xyz);
                float2 across2 = float2(dot(sun, input.right), dot(sun, input.up));
                float k = dot(sun, input.towardsViewer);
                float2 towards = dot(across2, across2) > 1e-6 ? normalize(across2) : float2(1.0, 0.0);
                float u = dot(input.disc, towards);
                float v = dot(input.disc, float2(-towards.y, towards.x));
                // The 12-gon's half-width at height v: between its corners at every 30 degrees from the top.
                float vc = clamp(v, -1.0, 1.0);
                float theta = asin(vc);
                float corner = min(floor((theta + 1.5707963) / Step), 5.0);
                float theta0 = -1.5707963 + corner * Step;
                float theta1 = theta0 + Step;
                float t = saturate((vc - sin(theta0)) / max(sin(theta1) - sin(theta0), 1e-5));
                float halfWidth = lerp(cos(theta0), cos(theta1), t);
                float edgeWidth = fwidth(u) * 1.2;
                float inside = (1.0 - smoothstep(halfWidth - edgeWidth, halfWidth, abs(u))) * step(abs(v), 1.0);
                if (inside <= 0.0)
                {
                    discard;
                }
                float lit = smoothstep(-k * halfWidth - edgeWidth, -k * halfWidth + edgeWidth, u);

                // Flat facets for tone only (twelve sectors in three rings), so the face stays low poly without a stepped edge.
                float r = length(input.disc);
                float angle = atan2(input.disc.y, input.disc.x) + 3.1415927;
                float facet = floor(angle / (Step * 2.0));
                float ring = min(floor(r * 3.0), 2.0);
                float2 centre = input.disc;
                float depth = sqrt(saturate(1.0 - r * r));
                float seas = Noise(centre * 2.3 + 4.1) * 0.6 + Noise(centre * 5.7 + 1.3) * 0.4;
                float albedo = lerp(1.0, 0.68, smoothstep(0.45, 0.7, seas)) * lerp(0.85, 1.0, depth) * (0.94 + 0.1 * Hash(float2(facet, ring)));
                float night = 1.0 - _Daylight;

                float3 colour = _Color.rgb * albedo * lit;
                float shine = _Earthshine * night * (1.0 - lit);
                colour += shine * float3(0.6, 0.7, 1.0) * albedo;
                // By night the whole disc stands in front of the sky; by day only the lit part shows, pale.
                float alpha = lerp(max(lit, saturate(shine * 12.0)), lit * _DayAlpha, _Daylight) * inside;
                return half4(colour, alpha);
            }
            ENDHLSL
        }
    }
}
