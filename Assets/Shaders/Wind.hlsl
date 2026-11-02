// The wind that sways the flora (GL239): grass, wheat and bushes (Clube/Grass), the voxel trees' leaves
// (Clube/Terrain Materials) and the trees far off (Clube/Distant Trees). Every shader asks the same
// function, so all of it sways in one breeze: a slow wave rolling across the ground, with a faster
// flutter on top and gusts that come and go.
#ifndef CLUBE_WIND_INCLUDED
#define CLUBE_WIND_INCLUDED

// Where the wind blows (x, z), and how fast the wave rolls (in the shaders' speed units).
static const float2 WindDirection = float2(0.8, 0.6);

// How far a point at positionWS sways across the ground, in metres, for a full sway of `strength`.
// `speed` sets how quickly it rocks. The wave depends only on where the point is, so two meshes'
// vertices at the same place move together.
float2 WindSway(float3 positionWS, float strength, float speed)
{
    float t = _Time.y * speed;
    float phase = t + dot(positionWS.xz, float2(0.35, 0.21));
    // Gusts: a broad, slow swell across the land.
    float gust = 0.6 + 0.4 * sin(_Time.y * 0.23 + dot(positionWS.xz, float2(0.021, 0.013)));
    float wave = sin(phase) + 0.5 * sin(phase * 2.3 + 1.7) + 0.2 * sin(phase * 5.1 + 0.4);
    return WindDirection * (wave * 0.6 + 0.4) * strength * gust;
}

#endif
