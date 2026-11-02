// Point and spot lights (GL294: torches) on the world's own shaders, which otherwise light only by the sun or moon.
// Include after URP's Lighting.hlsl, in a pass with:
//     #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
//     #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
// Diffuse only (wrapped a little, so a torch's glow reaches round the faces it lights), no shadows.
#ifndef CLUBE_ADDITIONAL_LIGHTS_INCLUDED
#define CLUBE_ADDITIONAL_LIGHTS_INCLUDED

half3 AdditionalDiffuse(float3 positionWS, float3 normalWS, float4 positionCS)
{
    half3 sum = 0;
#if defined(_ADDITIONAL_LIGHTS) || defined(_ADDITIONAL_LIGHTS_VERTEX) || defined(_CLUSTER_LIGHT_LOOP)
    InputData inputData = (InputData)0;
    inputData.positionWS = positionWS;
    inputData.normalWS = normalWS;
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(positionCS);
    uint lightCount = GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(lightCount)
        Light light = GetAdditionalLight(lightIndex, positionWS);
        half facing = saturate(dot(normalWS, light.direction) * 0.8 + 0.2);
        sum += light.color * light.distanceAttenuation * facing;
    LIGHT_LOOP_END
#endif
    return sum;
}

#endif
