#ifndef BONE_SQUAD_SHADER_UTILITY_INCLUDED
#define BONE_SQUAD_SHADER_UTILITY_INCLUDED

// Pixel centers from fragment SV_POSITION, in the current render target.
half Bayer4x4Threshold(float2 pixelPosition)
{
    static const uint ranks[16] = {
         0,  8,  2, 10,
        12,  4, 14,  6,
         3, 11,  1,  9,
        15,  7, 13,  5
    };
    uint2 cell = (uint2)pixelPosition & 3u;
    return (ranks[cell.y * 4u + cell.x] + 0.5h) / 16.0h;
}

#endif
