// Volumetric clouds (GL252, CloudLayer): drawn on a sphere round the camera; each pixel marches its view ray through a
// layer of cloud between _CloudBase and _CloudTop, curved round a planet of _PlanetRadius so it meets the horizon.
//
// Shape (after the Horizon Zero Dawn / Nubis approach): a cover map (the noise's red and green channels at a large
// scale, sliding slowly through the texture's depth so the weather changes) says where clouds stand; where it's high
// they stand taller. Within that, billowy Perlin-Worley noise eroded by Worley octaves gives them form, and a finer
// sample wears their edges into wisps at the base and billows on top. Everything drifts with the wind (_ShapeOffset,
// _Weather.xy) and boils slowly upwards (_Weather.w).
//
// Light: the main light (the sun by day, the moon at night) is marched a few steps towards the light for the cloud's
// shadow on itself, with a few octaves of cheaper, wider scattering standing in for light bouncing many times inside. A
// two-lobed Henyey-Greenstein phase scatters strongly forwards, so cloud edges in front of the sun or moon glow, with a
// little back-scatter; thin cloud edges facing away look darker ("powder"). The sky's ambient light (spherical
// harmonics, darkened at night by DayNightCycle) fills the shade, brighter towards the tops. Integrated per step so it
// holds up at any step length, ending once nearly opaque. The camera's depth texture stops the ray at the ground, and
// far clouds fade into the fog colour and out before _MaxDistance.
Shader "Clube/Clouds"
{
    Properties
    {
        [NoScaleOffset] _CloudNoise ("Cloud noise", 3D) = "white" {}
        _Albedo ("Brightness", Range(0.2, 2)) = 1
        _Ambient ("Sky light in the shade", Range(0, 3)) = 1.2
        _Forward ("Forward scattering", Range(0, 0.95)) = 0.65
        _Back ("Back scattering", Range(0, 0.6)) = 0.2
        _Powder ("Dark edges", Range(0, 1)) = 0.6
        _Haze ("Haze far off", Range(0, 1)) = 0.7
        _Erosion ("Edge erosion", Range(0, 1)) = 0.4
        _LightStep ("First light step (m)", Range(10, 300)) = 70
        [HideInInspector] _CloudBase ("Base", Float) = 1100
        [HideInInspector] _CloudTop ("Top", Float) = 2000
        [HideInInspector] _PlanetRadius ("Planet radius", Float) = 600000
        [HideInInspector] _Coverage ("Coverage", Range(0, 1)) = 0.42
        [HideInInspector] _Extinction ("Extinction", Float) = 0.035
        [HideInInspector] _Scales ("Shape, detail, weather scales", Vector) = (3200, 520, 16000, 0)
        [HideInInspector] _ShapeOffset ("Shape and detail offsets", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Weather ("Weather offset, slice, boil", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Steps ("Steps", Float) = 48
        [HideInInspector] _MaxDistance ("Max distance", Float) = 30000
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-300" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Clouds"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE3D(_CloudNoise);
            SAMPLER(sampler_CloudNoise);

            CBUFFER_START(UnityPerMaterial)
                float _Albedo;
                float _Ambient;
                float _Forward;
                float _Back;
                float _Powder;
                float _Haze;
                float _Erosion;
                float _LightStep;
                float _CloudBase;
                float _CloudTop;
                float _PlanetRadius;
                float _Coverage;
                float _Extinction;
                float4 _Scales;
                float4 _ShapeOffset;
                float4 _Weather;
                float _Steps;
                float _MaxDistance;
            CBUFFER_END

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

            float RemapTo01(float value, float low, float high)
            {
                return (value - low) / max(high - low, 1e-4);
            }

            // Henyey-Greenstein, scaled so scattering the same every way is 1.
            float HenyeyGreenstein(float cosine, float g)
            {
                float g2 = g * g;
                return (1.0 - g2) / pow(max(1.0 + g2 - 2.0 * g * cosine, 1e-4), 1.5);
            }

            float Phase(float cosine, float scale)
            {
                return lerp(HenyeyGreenstein(cosine, _Forward * scale), HenyeyGreenstein(cosine, -_Back * scale), 0.3);
            }

            // Where a ray from radius r (looking at cosine mu from straight up) crosses a shell of radius shell:
            // both crossings, or (-1, -1) if it misses.
            float2 Shell(float r, float mu, float shell)
            {
                float b = r * mu;
                float c = (r - shell) * (r + shell);
                float h = b * b - c;
                if (h < 0.0)
                {
                    return float2(-1.0, -1.0);
                }
                h = sqrt(h);
                return float2(-b - h, -b + h);
            }

            // 0 at the cloud base to 1 at the top, for a point measured from the planet's centre.
            float HeightFraction(float3 fromCentre)
            {
                return (length(fromCentre) - (_PlanetRadius + _CloudBase)) / (_CloudTop - _CloudBase);
            }

            // How much cloud stands over a point (0-1) and how tall it reaches (as a fraction of the layer).
            float Cover(float3 p)
            {
                float2 uv = (p.xz + _Weather.xy) / _Scales.z;
                float4 n = SAMPLE_TEXTURE3D_LOD(_CloudNoise, sampler_CloudNoise, float3(uv.x, _Weather.z, uv.y), 0);
                float weather = n.r * 0.7 + n.g * 0.3;
                return saturate(RemapTo01(weather, 1.0 - _Coverage - 0.12, 1.0 - _Coverage + 0.28));
            }

            // Flat bases, rounded tops; denser cover stands taller.
            float Profile(float f, float cover)
            {
                float top = 0.25 + 0.75 * cover;
                float bottom = saturate(f / 0.06);
                float upper = saturate((top - f) / (0.45 * top));
                return bottom * upper * upper * (3.0 - 2.0 * upper);
            }

            float Density(float3 p, float f, bool detailed)
            {
                if (f < 0.0 || f > 1.0)
                {
                    return 0.0;
                }
                float cover = Cover(p);
                if (cover <= 0.001)
                {
                    return 0.0;
                }
                float profile = Profile(f, cover);
                if (profile <= 0.001)
                {
                    return 0.0;
                }
                float3 shapeAt = float3(p.x + _ShapeOffset.x, p.y - _Weather.w, p.z + _ShapeOffset.y) / _Scales.x;
                float4 n = SAMPLE_TEXTURE3D_LOD(_CloudNoise, sampler_CloudNoise, shapeAt, 0);
                float worley = n.g * 0.625 + n.b * 0.25 + n.a * 0.125;
                float shape = saturate(RemapTo01(n.r, worley - 1.0, 1.0));
                float density = saturate(RemapTo01(shape * profile, 1.0 - cover * 0.85, 1.0));
                if (density <= 0.0 || !detailed)
                {
                    return density;
                }
                // Wear the edges: wisps underneath, billows on top.
                float3 detailAt = float3(p.x + _ShapeOffset.z, p.y - _Weather.w * 2.0, p.z + _ShapeOffset.w) / _Scales.y;
                float4 d = SAMPLE_TEXTURE3D_LOD(_CloudNoise, sampler_CloudNoise, detailAt, 0);
                float detail = d.g * 0.625 + d.b * 0.25 + d.a * 0.125;
                detail = lerp(1.0 - detail, detail, saturate(f * 4.0));
                return saturate(RemapTo01(density, detail * _Erosion, 1.0));
            }

            // Cloud between a point and the light, as density times metres.
            float LightDepth(float3 p, float3 centre, float3 towardsLight)
            {
                float depth = 0.0;
                float stepLength = _LightStep;
                float along = stepLength * 0.5;
                [unroll]
                for (int i = 0; i < 5; i++)
                {
                    float3 q = p + towardsLight * along;
                    float f = HeightFraction(q - centre);
                    if (f > 1.0 || f < 0.0)
                    {
                        break;
                    }
                    depth += Density(q, f, false) * stepLength;
                    along += stepLength * 1.3;
                    stepLength *= 1.7;
                }
                return depth;
            }

            float InterleavedGradientNoise(float2 pixel)
            {
                return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
            }

            float4 Fragment(Varyings input) : SV_Target
            {
                float3 origin = GetCameraPositionWS();
                float3 ray = normalize(input.direction);

                // How far the ground or anything solid is along this ray.
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float raw = SampleSceneDepth(screenUV);
                float sceneDistance = 1e9;
                #if UNITY_REVERSED_Z
                bool open = raw <= 1e-7;
                #else
                bool open = raw >= 1.0 - 1e-7;
                #endif
                if (!open)
                {
                    float3 forward = -UNITY_MATRIX_V[2].xyz;
                    sceneDistance = LinearEyeDepth(raw, _ZBufferParams) / max(dot(ray, forward), 1e-4);
                }

                // Where the ray is inside the layer.
                float r = _PlanetRadius + origin.y;
                float bottom = _PlanetRadius + _CloudBase;
                float top = _PlanetRadius + _CloudTop;
                float2 inner = Shell(r, ray.y, bottom);
                float2 outer = Shell(r, ray.y, top);
                float start;
                float end;
                if (r < bottom)
                {
                    start = inner.y;
                    end = outer.y;
                }
                else if (r <= top)
                {
                    start = 0.0;
                    end = inner.x > 0.0 ? inner.x : outer.y;
                }
                else
                {
                    if (outer.x < 0.0)
                    {
                        return 0;
                    }
                    start = outer.x;
                    end = inner.x > 0.0 ? inner.x : outer.y;
                }
                end = min(end, min(sceneDistance, _MaxDistance));
                if (end <= start)
                {
                    return 0;
                }

                float3 centre = float3(origin.x, -_PlanetRadius, origin.z);
                int steps = (int)clamp((end - start) / 20.0, 12.0, _Steps);
                float stepLength = (end - start) / steps;
                float t = start + stepLength * InterleavedGradientNoise(input.positionCS.xy);

                Light light = GetMainLight();
                float3 towardsLight = light.direction;
                float cosine = dot(ray, towardsLight);
                float3 lightColour = light.color;
                float3 skyTop = max(SampleSH(float3(0, 1, 0)), 0.0) * _Ambient;
                float3 skyBottom = skyTop * 0.4;

                float transmittance = 1.0;
                float3 colour = 0;
                float firstHit = -1.0;
                [loop]
                for (int i = 0; i < steps; i++)
                {
                    float3 p = origin + ray * t;
                    float f = HeightFraction(p - centre);
                    float density = Density(p, f, true);
                    if (density > 0.003)
                    {
                        if (firstHit < 0.0)
                        {
                            firstHit = t;
                        }
                        float sigma = density * _Extinction;
                        float shadow = LightDepth(p, centre, towardsLight) * _Extinction;

                        // Light reaching here, in octaves standing in for light scattered many times over.
                        float scattered = 0.0;
                        float a = 1.0;
                        float b = 1.0;
                        float c = 1.0;
                        [unroll]
                        for (int o = 0; o < 3; o++)
                        {
                            scattered += b * exp(-shadow * a) * Phase(cosine, c);
                            a *= 0.35;
                            b *= 0.5;
                            c *= 0.5;
                        }
                        float powder = lerp(1.0, 1.0 - exp(-sigma * 160.0), _Powder * (0.5 - 0.5 * cosine));
                        float3 lit = lightColour * scattered * powder + lerp(skyBottom, skyTop, saturate(f));

                        float stepTransmittance = exp(-sigma * stepLength);
                        colour += transmittance * lit * _Albedo * (1.0 - stepTransmittance);
                        transmittance *= stepTransmittance;
                        if (transmittance < 0.01)
                        {
                            break;
                        }
                    }
                    t += stepLength;
                }

                float alpha = 1.0 - transmittance;
                if (alpha <= 0.001)
                {
                    return 0;
                }
                // Far clouds take on the haze, then fade out before the end of their reach.
                float far = saturate(firstHit / _MaxDistance);
                colour = lerp(colour, unity_FogColor.rgb * alpha, far * _Haze);
                float fade = 1.0 - smoothstep(0.55, 1.0, far);
                return float4(colour, alpha) * fade;
            }
            ENDHLSL
        }
    }
}
