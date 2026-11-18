#ifndef BONE_SQUAD_BACKGROUND_SURFACE_INCLUDED
#define BONE_SQUAD_BACKGROUND_SURFACE_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
#include "../Utility.hlsl"

TEXTURE2D(_MetallicGlossMap);
SAMPLER(sampler_MetallicGlossMap);

// Keep the material layout identical across keywords and passes.
CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    float _ClipHeightWS;
    half4 _BaseColor;
    half _Metallic;
    half _Smoothness;
    half _BumpScale;
    half _Opacity;
    half _DiffuseHue;
    half _DiffuseSaturation;
    half _DiffuseValue;
    half _DiffuseContrast;
CBUFFER_END

void ClipAboveHeight(float worldY)
{
    clip(_ClipHeightWS - worldY);
}

// URP's Lit forward pass uses _Surface when writing the output alpha.
static const half _Surface = 0.0h;

void ApplyBackgroundCoverage(float2 uv, float2 pixelPosition)
{
#if defined(_DITHER_FADE)
    half mask = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).a;
    clip(saturate(mask * _BaseColor.a * _Opacity) - Bayer4x4Threshold(pixelPosition));
#endif
}

#if defined(_DIFFUSE_COLOR_ADJUST)
half3 AdjustBackgroundDiffuseColor(half3 rgb)
{
    // Keep HSV and the contrast midpoint in the artist-facing sRGB space.
#if !defined(UNITY_COLORSPACE_GAMMA)
    rgb = LinearToSRGB(rgb);
#endif
    half3 hsv = RgbToHsv(rgb);
    hsv.x = frac(hsv.x + _DiffuseHue / 360.0h);
    hsv.yz = saturate(hsv.yz * half2(_DiffuseSaturation, _DiffuseValue));
    rgb = saturate((HsvToRgb(hsv) - 0.5h) * _DiffuseContrast + 0.5h);
#if !defined(UNITY_COLORSPACE_GAMMA)
    rgb = SRGBToLinear(rgb);
#endif
    return rgb;
}
#endif

// Surface hook used by URP's Lit forward and lightmapping passes.
void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData surface)
{
    surface = (SurfaceData)0;
    half3 diffuse = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb;
#if defined(_DIFFUSE_COLOR_ADJUST)
    diffuse = AdjustBackgroundDiffuseColor(diffuse);
#endif
    surface.albedo = diffuse * _BaseColor.rgb;
    surface.alpha = 1.0h;
    surface.occlusion = 1.0h;
    surface.normalTS = SampleNormal(uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale);

#if defined(_METALLICSPECGLOSSMAP)
    half4 mask = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, uv);
    surface.metallic = mask.r;
    surface.smoothness = mask.a * _Smoothness;
#else
    surface.metallic = _Metallic;
    surface.smoothness = _Smoothness;
#endif
}

#endif
