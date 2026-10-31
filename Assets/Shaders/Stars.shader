// The night sky's stars (GL253, StarSky): drawn on a sphere round the camera. Each direction is turned into the
// celestial sphere's frame (_StarX, _StarY: right ascension 0 and 90 on the equator, _StarZ: the pole, all world
// directions set each frame by DayNightCycle), so the stars turn with the sky. Stars are hashed on a grid over the six
// faces of a cube: two layers, many faint ones and a few bright ones, white to blue and orange, more of them along a
// faint Milky Way band. They twinkle a little and fade out low down. Added onto the sky after it
// is drawn, without writing depth, so the ground hides them.
Shader "Clube/Stars"
{
    Properties
    {
        _Brightness ("Brightness", Range(0, 4)) = 1.6
        _Density ("Star density (1: about as many as the real sky)", Range(0, 4)) = 1.6
        _MilkyWay ("Milky Way", Range(0, 1)) = 0.4
        _Twinkle ("Twinkle", Range(0, 1)) = 0.35
        [HideInInspector] _StarX ("Right ascension 0", Vector) = (1, 0, 0, 0)
        [HideInInspector] _StarY ("Right ascension 90", Vector) = (0, 0, 1, 0)
        [HideInInspector] _StarZ ("Celestial pole", Vector) = (0, 1, 0, 0)
        [HideInInspector] _Night ("Night", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-450" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Stars"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Brightness;
                float _Density;
                float _MilkyWay;
                float _Twinkle;
                float4 _StarX;
                float4 _StarY;
                float4 _StarZ;
                float _Night;
            CBUFFER_END

            // The galactic pole and centre in the celestial frame (x: RA 0, y: RA 90, z: north pole).
            static const float3 GalacticPole = float3(-0.8675, -0.1981, 0.4560);
            static const float3 GalacticCentre = float3(-0.0550, -0.8738, -0.4833);

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.direction = positionWS - GetCameraPositionWS();
                return output;
            }

            uint3 Pcg3(uint3 v)
            {
                v = v * 1664525u + 1013904223u;
                v.x += v.y * v.z; v.y += v.z * v.x; v.z += v.x * v.y;
                v ^= v >> 16u;
                v.x += v.y * v.z; v.y += v.z * v.x; v.z += v.x * v.y;
                return v;
            }

            float3 Hash3(int3 cell)
            {
                return float3(Pcg3(uint3(cell + 65536))) * (1.0 / 4294967295.0);
            }

            float ValueNoise(float3 p)
            {
                int3 i = int3(floor(p));
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash3(i).x, b = Hash3(i + int3(1, 0, 0)).x, c = Hash3(i + int3(0, 1, 0)).x, d = Hash3(i + int3(1, 1, 0)).x;
                float e = Hash3(i + int3(0, 0, 1)).x, g = Hash3(i + int3(1, 0, 1)).x, h = Hash3(i + int3(0, 1, 1)).x, k = Hash3(i + int3(1, 1, 1)).x;
                return lerp(lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y), lerp(lerp(e, g, f.x), lerp(h, k, f.x), f.y), f.z);
            }

            // A point on the cube face the direction points through: the face's index and its 0-1 coordinates.
            float2 CubeFace(float3 s, out int face)
            {
                float3 a = abs(s);
                if (a.x >= a.y && a.x >= a.z)
                {
                    face = s.x > 0 ? 0 : 1;
                    return s.yz / a.x * 0.5 + 0.5;
                }
                if (a.y >= a.z)
                {
                    face = s.y > 0 ? 2 : 3;
                    return s.xz / a.y * 0.5 + 0.5;
                }
                face = s.z > 0 ? 4 : 5;
                return s.xy / a.z * 0.5 + 0.5;
            }

            // One layer of stars: at most one per grid cell, placed and sized by the cell's hash.
            float3 StarLayer(float2 uv, int face, float cells, float chance, float pixel, float brightest, float layer)
            {
                float2 p = uv * cells;
                int2 cell = int2(floor(p));
                float3 h = Hash3(int3(cell, face * 7 + (int)layer * 101));
                if (h.x > chance)
                {
                    return 0;
                }
                float3 h2 = Hash3(int3(cell.yx + 911, face + (int)layer * 13));
                float2 centre = float2(cell) + 0.2 + 0.6 * h.yz;
                float2 offset = p - centre;
                // Few bright stars, many faint ones.
                float magnitude = pow(h2.x, 6.0);
                float brightness = lerp(0.12, brightest, magnitude);
                float radius = pixel * (0.7 + 0.9 * magnitude);
                float glow = exp(-dot(offset, offset) / (radius * radius));
                // Twinkling: each star on its own beat.
                float twinkle = 1.0 - _Twinkle * (0.5 + 0.5 * sin(_Time.y * (2.0 + 5.0 * h2.y) + h2.z * 40.0));
                float3 colour = lerp(float3(1.0, 0.78, 0.6), float3(0.72, 0.82, 1.0), h2.z);
                colour = lerp(colour, float3(1, 1, 1), 0.5);
                return colour * glow * brightness * twinkle;
            }

            float4 Fragment(Varyings input) : SV_Target
            {
                float3 d = normalize(input.direction);
                // Into the celestial frame: the stars stay fixed there while the sky turns.
                float3 s = float3(dot(d, _StarX.xyz), dot(d, _StarY.xyz), dot(d, _StarZ.xyz));
                int face;
                float2 uv = CubeFace(s, face);
                // How much of a grid cell one pixel covers, at each layer's cell size.
                float pixelPerUnit = max(length(fwidth(s)) * 0.6, 1e-5);

                float horizon = smoothstep(-0.01, 0.22, d.y);
                float fade = _Night * _Night * horizon;
                if (fade <= 0.001)
                {
                    return 0;
                }

                // The Milky Way: a band round the galactic equator, brightest towards the centre, broken up by noise.
                float galactic = dot(s, GalacticPole);
                float band = exp(-galactic * galactic / 0.03);
                float centre = pow(saturate(0.5 + 0.5 * dot(s, GalacticCentre)), 3.0);
                float clouds = ValueNoise(s * 9.0) * 0.6 + ValueNoise(s * 23.0) * 0.4;
                float milky = band * (0.35 + 0.65 * centre) * saturate(clouds * 1.6 - 0.25);

                float faintCells = 220.0;
                float brightCells = 60.0;
                // About 3,500 faint stars over the whole sphere (more along the Milky Way) and a hundred or so bright ones.
                float3 colour = StarLayer(uv, face, faintCells, _Density * 0.012 * (0.4 + 1.6 * milky), pixelPerUnit * faintCells, 0.6, 0)
                              + StarLayer(uv, face, brightCells, _Density * 0.006, pixelPerUnit * brightCells, 2.4, 1);
                colour += float3(0.62, 0.68, 0.85) * milky * _MilkyWay * 0.05;

                return float4(colour * _Brightness * fade, 0);
            }
            ENDHLSL
        }
    }
}
