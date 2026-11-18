Shader "Bone Squad/Background PBR"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _Metallic("Metallic", Range(0, 1)) = 0
        _Smoothness("Smoothness", Range(0, 1)) = 0.5
        [Toggle(_METALLICSPECGLOSSMAP)] _UseMetallicMap("Use Metallic Smoothness Map", Float) = 0
        [NoScaleOffset] _MetallicGlossMap("Metallic (R), Smoothness (A)", 2D) = "white" {}
        [Toggle(_NORMALMAP)] _UseNormalMap("Use Normal Map", Float) = 0
        [Normal][NoScaleOffset] _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Strength", Range(0, 2)) = 1

        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull Mode", Float) = 2

        [Header(Diffuse Color)]
        [Toggle(_DIFFUSE_COLOR_ADJUST)] _UseDiffuseColorAdjust("Adjust Diffuse Color", Float) = 0
        _DiffuseHue("Hue Shift (Degrees)", Range(-180, 180)) = 0
        _DiffuseSaturation("Saturation", Range(0, 2)) = 1
        _DiffuseValue("Value", Range(0, 2)) = 1
        _DiffuseContrast("Contrast", Range(0, 2)) = 1

        [Toggle(_TREE_ROOT_VISIBILITY)] _TreeRootVisibility("Fade at object base", Float) = 0

        [Header(Transparency)]
        [Toggle(_CLIP_ABOVE_HEIGHT)] _UseHeightClip("Clip Above Height", Float) = 0
        _ClipHeightWS("Cutoff Height (World Y)", Float) = 0
        [Toggle(_DITHER_FADE)] _UseDitherFade("Dithered Opacity", Float) = 0
        _Opacity("Opacity", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }
        Cull [_Cull]
        ZWrite On
        Blend Off

        HLSLINCLUDE
        #pragma target 3.5
        #pragma shader_feature_local _DITHER_FADE
        #pragma shader_feature_local _CLIP_ABOVE_HEIGHT
        #include "BackgroundSurface.hlsl"
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForwardOnly" }

            // Bit 0 identifies the visible root-faded surface for the stage overlay.
            Stencil { Ref [_TreeRootVisibility] ReadMask 1 WriteMask 1 Comp Always Pass Replace }

            HLSLPROGRAM
            #pragma shader_feature_local_fragment _TREE_ROOT_VISIBILITY
            #pragma vertex LitPassVertex
            #pragma fragment BackgroundForwardFragment
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _DIFFUSE_COLOR_ADJUST
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ USE_LEGACY_LIGHTMAPS
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"

            #if defined(_TREE_ROOT_VISIBILITY)
            #include "StageVisibility.hlsl"
            TEXTURE2D(_StageTreeMask);
            SAMPLER(sampler_StageTreeMask);
            float4 _StageTreeU, _StageTreeV;
            half _StageTreeDarkness;
            TEXTURE2D(_StageTreeMask2);
            float4 _StageTreeU2, _StageTreeV2;
            half _StageTreeDarkness2;


            half TreeRootBrightness()
            {
                float4 root = float4(TransformObjectToWorld(float3(0, 0, 0)), 1);
                float2 uv = float2(dot(_StageTreeU, root), dot(_StageTreeV, root)) + 0.5;
                if (_StageTreeDarkness > 0 && all(uv >= 0.0) && all(uv <= 1.0))
                    return StageBrightness(SAMPLE_TEXTURE2D_LOD(_StageTreeMask, sampler_StageTreeMask, uv, 0).r, _StageTreeDarkness);

                float2 uv2 = float2(dot(_StageTreeU2, root), dot(_StageTreeV2, root)) + 0.5;
                if (_StageTreeDarkness2 > 0 && all(uv2 >= 0.0) && all(uv2 <= 1.0))
                    return StageBrightness(SAMPLE_TEXTURE2D_LOD(_StageTreeMask2, sampler_StageTreeMask, uv2, 0).r, _StageTreeDarkness2);
                return 1.0h;
            }
            #endif

            void BackgroundForwardFragment(Varyings input, out half4 color : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint renderingLayers : SV_Target1
            #endif
            )
            {
                ApplyBackgroundCoverage(input.uv, input.positionCS.xy);
            #if defined(_CLIP_ABOVE_HEIGHT)
                ClipAboveHeight(input.positionWS.y);
            #endif
                LitPassFragment(input, color
                #ifdef _WRITE_RENDERING_LAYERS
                    , renderingLayers
                #endif
                );
            #if defined(_TREE_ROOT_VISIBILITY)
                UNITY_SETUP_INSTANCE_ID(input);
                color.rgb *= TreeRootBrightness();
            #endif
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex BackgroundDepthVertex
            #pragma fragment BackgroundDepthFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #define BACKGROUND_SHADOW_PASS
            #include "BackgroundDepth.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ColorMask R

            HLSLPROGRAM
            #pragma vertex BackgroundDepthVertex
            #pragma fragment BackgroundDepthFragment
            #pragma multi_compile_instancing
            #include "BackgroundDepth.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormalsOnly" }

            HLSLPROGRAM
            #pragma vertex BackgroundDepthNormalsVertex
            #pragma fragment BackgroundDepthNormalsFragment
            #pragma shader_feature_local _NORMALMAP
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #if defined(_DITHER_FADE)
                #define REQUIRES_UV_INTERPOLATOR
            #endif
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitDepthNormalsPass.hlsl"

            Varyings BackgroundDepthNormalsVertex(Attributes input
            #if defined(_CLIP_ABOVE_HEIGHT)
                , out float worldY : TEXCOORD3
            #endif
            )
            {
            #if defined(_CLIP_ABOVE_HEIGHT)
                UNITY_SETUP_INSTANCE_ID(input);
                worldY = TransformObjectToWorld(input.positionOS.xyz).y;
            #endif
                return DepthNormalsVertex(input);
            }

            void BackgroundDepthNormalsFragment(Varyings input, out half4 normalWS : SV_Target0
            #if defined(_CLIP_ABOVE_HEIGHT)
                , float worldY : TEXCOORD3
            #endif
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint renderingLayers : SV_Target1
            #endif
            )
            {
            #if defined(_DITHER_FADE)
                ApplyBackgroundCoverage(input.uv, input.positionCS.xy);
            #endif
            #if defined(_CLIP_ABOVE_HEIGHT)
                ClipAboveHeight(worldY);
            #endif
                DepthNormalsFragment(input, normalWS
                #ifdef _WRITE_RENDERING_LAYERS
                    , renderingLayers
                #endif
                );
            }
            ENDHLSL
        }

        Pass
        {
            Name "Meta"
            Tags { "LightMode" = "Meta" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex BackgroundMetaVertex
            #pragma fragment BackgroundMetaFragment
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _DIFFUSE_COLOR_ADJUST
            #pragma shader_feature EDITOR_VISUALIZATION
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitMetaPass.hlsl"

            Varyings BackgroundMetaVertex(Attributes input
            #if defined(_CLIP_ABOVE_HEIGHT)
                , out float worldY : TEXCOORD3
            #endif
            )
            {
            #if defined(_CLIP_ABOVE_HEIGHT)
                UNITY_SETUP_INSTANCE_ID(input);
                worldY = TransformObjectToWorld(input.positionOS.xyz).y;
            #endif
                return UniversalVertexMeta(input);
            }

            half4 BackgroundMetaFragment(Varyings input
            #if defined(_CLIP_ABOVE_HEIGHT)
                , float worldY : TEXCOORD3
            #endif
            ) : SV_Target
            {
            #if defined(_CLIP_ABOVE_HEIGHT)
                ClipAboveHeight(worldY);
            #endif
                ApplyBackgroundCoverage(input.uv, input.positionCS.xy);
                return UniversalFragmentMetaLit(input);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
