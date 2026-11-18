#ifndef BONE_SQUAD_BACKGROUND_DEPTH_INCLUDED
#define BONE_SQUAD_BACKGROUND_DEPTH_INCLUDED

#if defined(BACKGROUND_SHADOW_PASS)
// Reuse URP's directional/punctual shadow bias and near-plane clamping.
#include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
#else
#include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
#endif

struct BackgroundDepthVaryings
{
    float4 positionCS : SV_POSITION;
#if defined(_CLIP_ABOVE_HEIGHT)
    float worldY : TEXCOORD1;
#endif
#if defined(_DITHER_FADE)
    float2 uv : TEXCOORD0;
#endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

BackgroundDepthVaryings BackgroundDepthVertex(Attributes input)
{
    BackgroundDepthVaryings output = (BackgroundDepthVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
#if defined(BACKGROUND_SHADOW_PASS)
    output.positionCS = GetShadowPositionHClip(input);
#if defined(_CLIP_ABOVE_HEIGHT)
    output.worldY = TransformObjectToWorld(input.positionOS.xyz).y;
#endif
#else
    output.positionCS = TransformObjectToHClip(input.position.xyz);
#if defined(_CLIP_ABOVE_HEIGHT)
    output.worldY = TransformObjectToWorld(input.position.xyz).y;
#endif
#endif
#if defined(_DITHER_FADE)
    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
#endif
    return output;
}

half BackgroundDepthFragment(BackgroundDepthVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
#if defined(_CLIP_ABOVE_HEIGHT)
    ClipAboveHeight(input.worldY);
#endif
#if defined(_DITHER_FADE)
    ApplyBackgroundCoverage(input.uv, input.positionCS.xy);
#endif
    return input.positionCS.z;
}

#endif
