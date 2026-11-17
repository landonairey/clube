// Water (GL309, GL312, GL315; WaterView): the marching-cubes surface of the water in the voxel data, low-poly like the
// terrain. Every triangle is one facet: it shades flat and shimmers as one, between a muted deep and light blue-green and
// now and then a pale fleck. On running water the light bands move along the flow (UV0), quicker where the run is steeper
// (the flow's length); on ponds and lakes facets twinkle slowly at random and now and then one glimmers; falling water
// streams downwards (GL318: still water no longer twinkles at random: its facets lighten as the ripples tip them, and
// facets meeting the bank lap with foam, UV3). Ripples move the surface in the drawing only (the water's volume never changes): every vertex rises
// and falls by waves from its place in the world and the river's flow there (UV2), the same for every triangle sharing
// it, so the surface never cracks, and each facet's tilt follows. GL673: how much water lies behind a facet (from the
// camera's depth texture) darkens it towards the abyss colour and makes it opaque, so the sea floor fades from sight a
// few metres down and the ground's end past the map edge is never seen; deep open water rolls with long swells.
Shader "Clube/Water"
{
    Properties
    {
        _Deep ("Deep", Color) = (0.15, 0.3, 0.37, 1)
        _Light ("Light", Color) = (0.33, 0.5, 0.55, 1)
        _Foam ("Foam", Color) = (0.82, 0.87, 0.86, 1)
        _Alpha ("Alpha", Range(0, 1)) = 0.68
        _FlowBand ("Band length (m)", Float) = 2.2
        _FlowSpeed ("Band speed", Float) = 0.45
        _Twinkle ("Still water twinkle speed", Float) = 0.05
        _Ripple ("Ripple height (m)", Float) = 0.035
        _Glimmer ("Lake glimmer", Range(0, 3)) = 1.0
        _ShoreFoam ("Shore foam", Range(0, 1)) = 0.7
        _FoamSpeed ("Shore foam speed", Float) = 1.1
        _Abyss ("Abyss (deep water)", Color) = (0.03, 0.09, 0.14, 1)
        _Murk ("Murk depth (m to mostly hide the floor)", Float) = 3.5
        _Swell ("Deep swell strength", Range(0, 1)) = 0.6
        _SwellFrom ("Swell starts at depth (m)", Float) = 3
        [HideInInspector] _ClipCovered ("Clip drawn chunk columns (far sheets)", Float) = 0
        [HideInInspector] _CoverageMask ("Chunk coverage", 2D) = "black" {}
        [HideInInspector] _Coverage ("Coverage origin, column width, size", Vector) = (0, 0, 1, 0)
        [HideInInspector] _Detail ("Detail square: centre x, z, half width, side (0 none, 1 inside, 2 outside, 3 everywhere and level)", Vector) = (0, 0, 0, 0)
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Assets/Shaders/AdditionalLights.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Deep;
                half4 _Light;
                half4 _Foam;
                half _Alpha;
                float _FlowBand;
                float _FlowSpeed;
                float _Twinkle;
                float _Ripple;
                half _Glimmer;
                half _ShoreFoam;
                float _FoamSpeed;
                float _ClipCovered;
                float4 _Coverage;
                float4 _Detail;
                half4 _Abyss;
                float _Murk;
                half _Swell;
                float _SwellFrom;
            CBUFFER_END
            TEXTURE2D(_CoverageMask);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 flow : TEXCOORD0;
                float4 look : TEXCOORD1;
                float4 ripple : TEXCOORD2;
                float2 bank : TEXCOORD3;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                nointerpolation float2 flow : TEXCOORD1;
                nointerpolation float4 look : TEXCOORD2;
                float fog : TEXCOORD3;
                nointerpolation float bank : TEXCOORD4;
            };

            float Hash(float3 p)
            {
                return frac(sin(dot(p, float3(127.1, 311.7, 74.7))) * 43758.5453);
            }

            // The facet's colour: a level 0-1 from deep to light, and a pale fleck now and then.
            half3 Shimmer(float2 flow, float3 centre, float kind, float random)
            {
                float t = _Time.y;
                float level;
                float foam;
                float speed = length(flow);
                if (kind > 2.5)
                {
                    // Falling water: bands pour down.
                    float wave = frac(centre.y / (_FlowBand * 0.5) + t * _FlowSpeed * 4.0 + random * 0.3);
                    level = smoothstep(0.4, 0.8, wave) * (1.0 - smoothstep(0.85, 1.0, wave));
                    foam = step(0.8, wave) * step(0.55, random);
                }
                else if (kind < 1.5 && speed > 0.001)
                {
                    // Bands down the run: the phase falls along the flow, so the light moves downstream, faster where steeper.
                    float2 along = flow / speed;
                    float wave = frac(dot(centre.xz, along) / _FlowBand - t * _FlowSpeed * speed + random * 0.3);
                    level = smoothstep(0.55, 0.85, wave) * (1.0 - smoothstep(0.9, 1.0, wave));
                    foam = step(0.92, wave) * step(0.9 - min(speed, 2.0) * 0.1, random);
                }
                else
                {
                    // Still water: each facet twinkles slowly on its own beat.
                    float beat = frac(t * _Twinkle * (1.0 + random * 1.5) + random * 7.0);
                    level = smoothstep(0.8, 0.9, beat) * (1.0 - smoothstep(0.92, 1.0, beat));
                    foam = step(0.994, beat) * step(0.85, random);
                }
                half3 color = lerp(_Deep.rgb, _Light.rgb, level * (0.7 + 0.3 * random));
                return lerp(color, _Foam.rgb, foam * 0.6);
            }

            // How far the surface rises or falls at a point: crossing wavelets on still water, waves running downstream
            // where it flows, blended by how much of it is river.
            float Ripple(float2 p, float4 ripple)
            {
                float t = _Time.y;
                float still = sin(dot(p, float2(0.8, 0.6)) * 2.1 + t * 0.9) * 0.45
                            + sin(dot(p, float2(-0.55, 0.83)) * 2.9 + t * 1.2) * 0.35
                            + sin(dot(p, float2(0.15, -0.99)) * 3.7 + t * 1.5) * 0.2;
                float speed = length(ripple.xy);
                float running = still;
                if (speed > 0.001)
                {
                    float2 along = ripple.xy / speed;
                    float2 across = float2(-along.y, along.x);
                    float a = dot(p, along);
                    float c = dot(p, across);
                    running = sin(a * 2.4 - t * (1.6 + 1.5 * speed)) * 0.55
                            + sin(a * 4.1 + c * 1.7 - t * (2.2 + 2.0 * speed)) * 0.3
                            + sin(c * 3.3 + a * 0.8 - t * 1.0) * 0.15;
                }
                float river = saturate(ripple.z);
                return lerp(still, running, river) * _Ripple;
            }

            // Deep ocean swell (GL673): the slope of long rolling waves at a point, drawn in the shading only, so it shows
            // the same on every sheet however coarse its triangles are.
            float2 SwellSlope(float2 p)
            {
                float t = _Time.y;
                float2 d0 = float2(0.83, 0.56), d1 = float2(0.35, 0.94), d2 = float2(0.97, -0.24), d3 = float2(-0.5, 0.87);
                float2 slope = d0 * cos(dot(p, d0) * 0.33 + t * 0.95) * 0.10;
                slope += d1 * cos(dot(p, d1) * 0.52 + t * 1.25) * 0.083;
                slope += d2 * cos(dot(p, d2) * 0.81 + t * 1.6) * 0.065;
                slope += d3 * cos(dot(p, d3) * 1.7 + t * 2.3) * 0.05;
                return slope;
            }

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                // Far water (GL626): the detailed sheets ripple inside the detail square round the viewer, dying away
                // towards its edge, where the coarse sheets (which never ripple, so their big triangles stay level and
                // meet exactly) take over.
                float ripple = Ripple(positionWS.xz, input.ripple);
                if (_Detail.w > 0.5)
                {
                    float2 off = abs(positionWS.xz - _Detail.xy);
                    ripple *= _Detail.w < 1.5 ? saturate((_Detail.z - max(off.x, off.y)) / 48.0) : 0.0;
                }
                positionWS.y += ripple;
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.flow = input.flow;
                output.look = input.look;
                output.bank = input.bank.x;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                // Far sheets (GL323) leave out the chunk columns the world draws, whose own water is drawn there.
                if (_Detail.w > 0.5 && _Detail.w < 2.5)
                {
                    // Detailed far sheets are drawn inside the detail square, coarse ones outside (a hair's overlap
                    // rather than a gap).
                    float2 off = abs(input.positionWS.xz - _Detail.xy);
                    float outside = max(off.x, off.y) - _Detail.z;
                    clip(_Detail.w < 1.5 ? -outside : outside + 0.05);
                }
                if (_ClipCovered > 0.5)
                {
                    int2 texel = (int2)floor((input.positionWS.xz - _Coverage.xy) / _Coverage.z);
                    int size = (int)_Coverage.w;
                    bool inside = all(texel >= 0) && all(texel < size);
                    clip(inside && LOAD_TEXTURE2D(_CoverageMask, texel).r > 0.5 ? -1.0 : 1.0);
                }
                float3 centre = input.look.xyz;
                float kind = input.look.w;
                float random = Hash(floor(centre * 64.0) / 64.0);

                // Flat: the facet's own plane, ripples and all.
                float3 normalWS = normalize(cross(ddy(input.positionWS), ddx(input.positionWS)));
                normalWS *= kind > 2.5 ? 1.0 : sign(normalWS.y + 1e-5);

                // How much water lies behind this facet along the view (GL673), from the depth the ground left.
                float2 screenUV = input.positionCS.xy / _ScaledScreenParams.xy;
                float sceneEye = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float waterEye = LinearEyeDepth(input.positionCS.z, _ZBufferParams);
                float thickness = max(0.0, sceneEye - waterEye);
                float deep = 1.0 - exp(-thickness / max(_Murk, 0.01));
                float open = smoothstep(_SwellFrom, _SwellFrom * 4.0, thickness) * (kind < 2.5 ? 1.0 : 0.0);
                if (open > 0.0)
                {
                    float2 slope = SwellSlope(input.positionWS.xz) * (_Swell * open);
                    normalWS = normalize(normalWS + float3(-slope.x, 0.0, -slope.y));
                }

                half3 shimmer;
                if (kind > 1.5 && kind < 2.5)
                {
                    // Still water (GL318): no random flecks; a facet is lighter the more the rolling ripples tip it one
                    // way, so the light moves with the waves, smoothly.
                    float tip = dot(normalWS.xz, float2(0.6, 0.8)) / max(_Ripple * 3.0, 0.001);
                    shimmer = lerp(_Deep.rgb, _Light.rgb, smoothstep(-0.1, 1.0, tip) * (0.8 + 0.2 * random));
                }
                else
                {
                    shimmer = Shimmer(input.flow, centre, kind, random);
                }

                // Deep water darkens towards the abyss colour; the swell's faces turned to the light catch a little of it.
                half crest = saturate(dot(normalWS.xz, float2(0.6, 0.8)) * 4.0) * open;
                shimmer = lerp(shimmer, lerp(_Abyss.rgb, _Light.rgb, crest * 0.45), deep * 0.85);

                // Foam where the water meets the bank (GL318): facets touching the ground whiten as small waves lap in.
                float bank = input.bank;
                if (bank > 0.5)
                {
                    float lap = sin(_Time.y * _FoamSpeed + dot(centre.xz, float2(0.7, 0.45)) * 1.8 + random * 2.0) * 0.5 + 0.5;
                    float foam = smoothstep(0.3, 0.8, lap) * (0.55 + 0.45 * random);
                    shimmer = lerp(shimmer, _Foam.rgb, foam * _ShoreFoam);
                }

                Light mainLight = GetMainLight();
                half3 direct = mainLight.color * (saturate(dot(normalWS, mainLight.direction)) * 0.55 + 0.45);
                half3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
                // A soft glint, from the facet's tilt alone, so it moves with the ripples instead of flickering.
                half glint = pow(saturate(dot(reflect(-mainLight.direction, normalWS), viewDir)), 24.0) * (0.12 + 0.08 * _Glimmer);
                half3 ambient = SampleSH(half3(0, 1, 0));
                direct += AdditionalDiffuse(input.positionWS, normalWS, input.positionCS);
                half3 color = shimmer * (direct + ambient) + mainLight.color * glint;
                color = MixFog(color, input.fog);
                // Deep water hides the floor (GL673): nearly opaque a few metres down.
                half alpha = lerp(_Alpha, 0.985, deep);
                return half4(color, saturate(alpha + glint * 0.3 + bank * 0.08));
            }
            ENDHLSL
        }
    }
}
