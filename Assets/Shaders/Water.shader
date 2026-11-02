// Rivers and lakes (GL309, GL312; WaterView): a low-poly water surface of flat facets that shimmer between blue, light
// blue and now and then white. On rivers the light bands run down the river along its flow (UV0), so the water shows which
// way it runs; on ponds and lakes facets twinkle at random, and now and then one glimmers in the sun.
// The mesh has two triangles per metre cell (the diagonal alternating, as WaterView lays them); this shader cuts each into
// four at its edges' middles, in the drawing only: from a pixel's place on the grid it finds its triangle and quarter, and
// gives the quarter its own colour and its own tilt.
// Ripples move the surface in the drawing only (the water's volume never changes): every corner rises and falls by waves
// that run downstream on rivers and cross each other on lakes, from the flow at the corner and how much of it is river
// (UV3, the same for every triangle sharing the corner, so the surface never cracks); each quarter tilts by the ripples at
// its own corners and catches the light.
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
        _Ripple ("Ripple height (m)", Float) = 0.05
        _Glimmer ("Lake glimmer", Range(0, 3)) = 1.6
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
                float _Ripple;
                half _Glimmer;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 flow : TEXCOORD0;
                float2 kind : TEXCOORD2;
                float4 ripple : TEXCOORD3;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 grid : TEXCOORD1;
                float4 ripple : TEXCOORD2;
                nointerpolation float3 look : TEXCOORD3;
                float fog : TEXCOORD4;
            };

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            // The facet's colour: a level 0-1 from deep to light, and foam on top of that.
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
                    // Lakes: each facet twinkles on its own beat.
                    float beat = frac(t * (0.12 + random * 0.2) + random * 7.0);
                    level = smoothstep(0.78, 0.9, beat) * (1.0 - smoothstep(0.92, 1.0, beat));
                    foam = step(0.97, beat) * step(0.8, random);
                }
                half3 color = lerp(_Deep.rgb, _Light.rgb, level * (0.75 + 0.25 * random));
                return lerp(color, _Foam.rgb, foam);
            }

            // How far the surface rises or falls at a point: crossing wavelets on still water, waves running downstream
            // where it flows, blended by how much of it is river.
            float Ripple(float2 p, float4 ripple)
            {
                float t = _Time.y;
                float still = sin(dot(p, float2(0.8, 0.6)) * 2.1 + t * 1.3) * 0.45
                            + sin(dot(p, float2(-0.55, 0.83)) * 2.9 + t * 1.7) * 0.35
                            + sin(dot(p, float2(0.15, -0.99)) * 3.7 + t * 2.2) * 0.2;
                float speed = length(ripple.xy);
                float running = still;
                if (speed > 0.001)
                {
                    float2 along = ripple.xy / speed;
                    float2 across = float2(-along.y, along.x);
                    float a = dot(p, along);
                    float c = dot(p, across);
                    running = sin(a * 2.4 - t * (2.5 + 2.5 * speed)) * 0.55
                            + sin(a * 4.1 + c * 1.7 - t * (3.5 + 3.0 * speed)) * 0.3
                            + sin(c * 3.3 + a * 0.8 - t * 1.5) * 0.15;
                }
                float river = saturate(ripple.z);
                return lerp(still, running, river) * _Ripple * (1.0 + 0.3 * river);
            }

            float3 Barycentric(float2 p, float2 a, float2 b, float2 c)
            {
                float2 v0 = b - a, v1 = c - a, v2 = p - a;
                float d = v0.x * v1.y - v1.x * v0.y;
                float v = (v2.x * v1.y - v1.x * v2.y) / d;
                float w = (v0.x * v2.y - v2.x * v0.y) / d;
                return float3(1.0 - v - w, v, w);
            }

            // The quarter of a mesh triangle a grid point lies in: its three corners (x, z), as WaterView lays the cell's
            // two triangles (diagonal a-c on even cells, b-d on odd ones), cut at their edges' middles.
            void Quarter(float2 p, out float2 s0, out float2 s1, out float2 s2)
            {
                float2 cell = floor(p);
                float2 f = p - cell;
                float2 a = float2(0, 0), b = float2(1, 0), c = float2(1, 1), d = float2(0, 1);
                float2 P, Q, R;
                bool even = fmod(abs(cell.x + cell.y), 2.0) < 0.5;
                if (even)
                {
                    if (f.y > f.x) { P = a; Q = d; R = c; } else { P = a; Q = c; R = b; }
                }
                else
                {
                    if (f.x + f.y < 1.0) { P = a; Q = d; R = b; } else { P = b; Q = d; R = c; }
                }
                float3 l = Barycentric(f, P, Q, R);
                float2 pq = (P + Q) * 0.5, qr = (Q + R) * 0.5, rp = (R + P) * 0.5;
                if (l.x > 0.5) { s0 = P; s1 = pq; s2 = rp; }
                else if (l.y > 0.5) { s0 = pq; s1 = Q; s2 = qr; }
                else if (l.z > 0.5) { s0 = rp; s1 = qr; s2 = R; }
                else { s0 = pq; s1 = qr; s2 = rp; }
                s0 += cell;
                s1 += cell;
                s2 += cell;
            }

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                float3 positionOS = input.positionOS.xyz;
                positionOS.y += Ripple(positionOS.xz, input.ripple);
                output.positionWS = TransformObjectToWorld(positionOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.grid = input.positionOS.xz;
                output.ripple = input.ripple;
                output.look = float3(input.flow, input.kind.x);
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float2 s0, s1, s2;
                Quarter(input.grid, s0, s1, s2);
                float2 centre = (s0 + s1 + s2) / 3.0;
                float random = Hash(centre * 7.13);
                bool lake = input.look.z > 1.5;
                half3 shimmer = Shimmer(input.look.xy, centre, lake ? 0.0 : 1.0, random);

                // The quarter's tilt: the plane through the ripples at its own corners, on the slope of the mesh triangle.
                float3 c0 = float3(s0.x, Ripple(s0, input.ripple), s0.y);
                float3 c1 = float3(s1.x, Ripple(s1, input.ripple), s1.y);
                float3 c2 = float3(s2.x, Ripple(s2, input.ripple), s2.y);
                float3 tilt = normalize(cross(c1 - c0, c2 - c0));
                tilt *= sign(tilt.y);
                float3 slope = normalize(cross(ddy(input.positionWS), ddx(input.positionWS)));
                slope *= sign(slope.y);
                float3 normalWS = normalize(slope + TransformObjectToWorldNormal(tilt) - float3(0, 1, 0));

                Light mainLight = GetMainLight();
                half3 direct = mainLight.color * (saturate(dot(normalWS, mainLight.direction)) * 0.6 + 0.4);
                half3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
                half glint = pow(saturate(dot(reflect(-mainLight.direction, normalWS), viewDir)), 48.0);
                // Lakes glimmer: now and then a facet flashes with the sun's (or moon's) light.
                half flash = 0;
                if (lake)
                {
                    float beat = frac(_Time.y * (0.25 + random * 0.6) + random * 31.0);
                    flash = step(0.985, beat) * step(0.5, random) * _Glimmer;
                    glint *= 1.0 + _Glimmer;
                }
                half3 ambient = SampleSH(half3(0, 1, 0));
                direct += AdditionalDiffuse(input.positionWS, normalWS, input.positionCS);
                half3 color = shimmer * (direct + ambient) + mainLight.color * (glint * 0.6 + flash);
                color = MixFog(color, input.fog);
                return half4(color, saturate(_Alpha + flash * 0.2));
            }
            ENDHLSL
        }
    }
}
