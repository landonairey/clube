// Roads seen from afar (GL718): the terrain past the world's chunks has no road voxels, so it colours its ground from a
// picture of the map's roads (RoadMask, set as globals by TerrainRoadTint). _RoadMask holds the road colour times its
// coverage in rgb and the coverage in alpha (premultiplied); _RoadMaskRect is its corner (x, z), 1 over its side, and
// w 1 when there is one.
#ifndef CLUBE_ROAD_TINT
#define CLUBE_ROAD_TINT

TEXTURE2D(_RoadMask);
SAMPLER(sampler_RoadMask);
float4 _RoadMaskRect;

half3 RoadTint(half3 albedo, float2 xz)
{
    if (_RoadMaskRect.w < 0.5)
    {
        return albedo;
    }
    float2 uv = (xz - _RoadMaskRect.xy) * _RoadMaskRect.z;
    if (any(uv < 0.0) || any(uv > 1.0))
    {
        return albedo;
    }
    half4 road = SAMPLE_TEXTURE2D_LOD(_RoadMask, sampler_RoadMask, uv, 0);
    return albedo * (1.0 - road.a) + road.rgb;
}

#endif
