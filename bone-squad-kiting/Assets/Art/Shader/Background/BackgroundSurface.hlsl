#ifndef BONE_SQUAD_BACKGROUND_SURFACE_INCLUDED
#define BONE_SQUAD_BACKGROUND_SURFACE_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
#include "../Utility.hlsl"

TEXTURE2D(_MetallicGlossMap);
SAMPLER(sampler_MetallicGlossMap);

// Keep the material layout identical across keywords and passes.
CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4 _BaseColor;
    half _Metallic;
    half _Smoothness;
    half _BumpScale;
    half _Opacity;
CBUFFER_END

// URP's Lit forward pass uses _Surface when writing the output alpha.
static const half _Surface = 0.0h;

void ApplyBackgroundCoverage(float2 uv, float2 pixelPosition)
{
#if defined(_DITHER_FADE)
    half mask = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).a;
    clip(saturate(mask * _BaseColor.a * _Opacity) - Bayer4x4Threshold(pixelPosition));
#endif
}

// Surface hook used by URP's Lit forward and lightmapping passes.
void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData surface)
{
    surface = (SurfaceData)0;
    surface.albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb * _BaseColor.rgb;
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
