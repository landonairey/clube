// Grass seen from afar (GL257): what colour grass ground and far tufts take on. GrassScatter's GrassLawnMap keeps a
// map of it round the focus (_GrassLawnMap over _GrassLawnMapRect: x and z of its corner, 1 over its width, 1 when
// shown), and its average (_GrassLawnColor) stands in beyond the map. Linear colours. All globals.
#ifndef CLUBE_GRASS_LAWN
#define CLUBE_GRASS_LAWN

TEXTURE2D(_GrassLawnMap);
SAMPLER(sampler_GrassLawnMap);
float4 _GrassLawnMapRect;
float4 _GrassLawnColor;

// The colour grass shows from afar at a world position's x, z; it eases into the average near the map's edge.
half3 GrassLawnAt(float2 xz)
{
    half3 colour = _GrassLawnColor.rgb;
    if (_GrassLawnMapRect.w > 0.5)
    {
        float2 uv = (xz - _GrassLawnMapRect.xy) * _GrassLawnMapRect.z;
        float2 edge = min(uv, 1.0 - uv);
        float inside = saturate(min(edge.x, edge.y) * 12.0);
        if (inside > 0.0)
        {
            half3 mapped = SAMPLE_TEXTURE2D_LOD(_GrassLawnMap, sampler_GrassLawnMap, saturate(uv), 0).rgb;
            colour = lerp(colour, mapped, inside);
        }
    }
    return colour;
}

#endif
