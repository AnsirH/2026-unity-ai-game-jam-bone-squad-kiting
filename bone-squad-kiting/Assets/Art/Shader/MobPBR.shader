Shader "Bone Squad/Mob PBR"
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

        [Toggle(_MOB_EMISSION_MAP)] _UseEmissionMap("Use Emission Map", Float) = 0
        [NoScaleOffset] _EmissionMap("Emission Map (RGB)", 2D) = "white" {}
        [HDR] _EmissionColor("Emission Color", Color) = (0, 0, 0, 1)
        _Opacity("Opacity", Range(0, 1)) = 1

        [Header(Hit Flash)]
        [HDR] _HitFlashColor("Hit Flash Color", Color) = (1, 1, 1, 1)
        _HitFlashIntensity("Hit Flash Intensity", Range(0, 10)) = 3
        [HideInInspector] _HitFlashAmount("Hit Flash Amount", Range(0, 1)) = 0

        [Header(Fresnel)]
        [HDR] _FresnelColor("Fresnel Color", Color) = (1, 1, 1, 1)
        _FresnelIntensity("Fresnel Intensity", Range(0, 5)) = 0.5
        _FresnelPower("Fresnel Power", Range(0.25, 12)) = 3

        [Header(Dissolve)]
        _DissolveAmount("Dissolve Amount", Range(0, 1)) = 0
        _DissolveScale("Noise Scale", Range(1, 128)) = 24
        [HDR] _DissolveEdgeColor("Edge Color", Color) = (2, 0.5, 0.1, 1)
        _DissolveEdgeWidth("Edge Width", Range(0, 0.25)) = 0.06
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }
        Cull [_Cull]
        ZWrite On
        Blend Off

        HLSLINCLUDE
        #pragma target 3.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
        #include "Utility.hlsl"

        TEXTURE2D(_MetallicGlossMap);
        SAMPLER(sampler_MetallicGlossMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor, _EmissionColor, _FresnelColor, _DissolveEdgeColor;
            half _Metallic, _Smoothness, _BumpScale, _Opacity;
            half4 _HitFlashColor;
            half _HitFlashIntensity, _HitFlashAmount;
            half _FresnelIntensity, _FresnelPower;
            float _DissolveAmount, _DissolveScale, _DissolveEdgeWidth;
        CBUFFER_END

        float NoiseCorner(float2 cell)
        {
            float3 p = frac(cell.xyx * 0.1031);
            p += dot(p, p.yzx + 33.33);
            return frac((p.x + p.y) * p.z);
        }

        float DissolveNoise(float2 uv)
        {
            float2 p = uv * _DissolveScale;
            float2 cell = floor(p);
            float2 t = frac(p);
            t = t * t * (3.0 - 2.0 * t);
            return lerp(lerp(NoiseCorner(cell), NoiseCorner(cell + float2(1, 0)), t.x),
                        lerp(NoiseCorner(cell + float2(0, 1)), NoiseCorner(cell + 1.0), t.x), t.y);
        }

        // All raster passes discard the same UV regions. Only the lit pass uses the edge weight.
        half ApplyMobCoverage(float2 uv, float2 pixelPosition)
        {
            half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).a * _BaseColor.a * _Opacity;
            clip(alpha - Bayer4x4Threshold(pixelPosition));
            if (_DissolveAmount <= 0.0)
                return 0.0h;

            clip(_DissolveAmount < 1.0 ? 1.0 : -1.0);
            float distanceToEdge = DissolveNoise(uv) - _DissolveAmount;
            clip(distanceToEdge);
            return _DissolveEdgeWidth > 0.0
                ? 1.0 - saturate(distanceToEdge / max(_DissolveEdgeWidth, 0.00001)) : 0.0;
        }

        static const half _Surface = 0.0h;
        void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData surface)
        {
            surface = (SurfaceData)0;
            surface.albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb * _BaseColor.rgb;
            surface.normalTS = SampleNormal(uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale);
            surface.metallic = _Metallic;
            surface.smoothness = _Smoothness;
        #if defined(_METALLICSPECGLOSSMAP)
            half4 mask = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, uv);
            surface.metallic = mask.r;
            surface.smoothness *= mask.a;
        #endif
            surface.emission = _EmissionColor.rgb;
        #if defined(_MOB_EMISSION_MAP)
            surface.emission *= SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv).rgb;
        #endif
            surface.emission += _HitFlashColor.rgb * (_HitFlashIntensity * saturate(_HitFlashAmount));
            surface.occlusion = 1.0h;
            surface.alpha = 1.0h;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForwardOnly" }

            HLSLPROGRAM
            #pragma vertex LitPassVertex
            #pragma fragment MobFragment
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _MOB_EMISSION_MAP
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

            void MobFragment(Varyings input, out half4 color : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint renderingLayers : SV_Target1
            #endif
            )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half edge = ApplyMobCoverage(input.uv, input.positionCS.xy);
                SurfaceData surface;
                InitializeStandardLitSurfaceData(input.uv, surface);
                InputData lighting;
                InitializeInputData(input, surface.normalTS, lighting);
                InitializeBakedGIData(input, lighting);
                half facing = saturate(abs(dot(lighting.normalWS, lighting.viewDirectionWS)));
                surface.emission += _FresnelColor.rgb * (_FresnelColor.a * _FresnelIntensity * pow(1.0h - facing, _FresnelPower));
                surface.emission += _DissolveEdgeColor.rgb * edge;
                color = UniversalFragmentPBR(lighting, surface);
                color.rgb = MixFog(color.rgb, lighting.fogCoord);
                color.a = 1.0h;
            #ifdef _WRITE_RENDERING_LAYERS
                renderingLayers = EncodeMeshRenderingLayer();
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
            #pragma vertex MobShadowVertex
            #pragma fragment MobShadowFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"

            Varyings MobShadowVertex(Attributes input, out float2 uv : TEXCOORD1)
            {
                uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                return ShadowPassVertex(input);
            }

            half MobShadowFragment(Varyings input, float2 uv : TEXCOORD1) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                ApplyMobCoverage(uv, input.positionCS.xy);
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ColorMask R

            HLSLPROGRAM
            #pragma vertex MobDepthOnlyVertex
            #pragma fragment MobDepthOnlyFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"

            Varyings MobDepthOnlyVertex(Attributes input, out float2 uv : TEXCOORD1)
            {
                uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                return DepthOnlyVertex(input);
            }

            half MobDepthOnlyFragment(Varyings input, float2 uv : TEXCOORD1) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                ApplyMobCoverage(uv, input.positionCS.xy);
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormalsOnly" }

            HLSLPROGRAM
            #pragma vertex DepthNormalsVertex
            #pragma fragment MobDepthNormalsFragment
            #pragma shader_feature_local _NORMALMAP
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #define REQUIRES_UV_INTERPOLATOR
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitDepthNormalsPass.hlsl"

            void MobDepthNormalsFragment(Varyings input, out half4 normalWS : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint renderingLayers : SV_Target1
            #endif
            )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                ApplyMobCoverage(input.uv, input.positionCS.xy);
                DepthNormalsFragment(input, normalWS
                #ifdef _WRITE_RENDERING_LAYERS
                    , renderingLayers
                #endif
                );
            }
            ENDHLSL
        }
    }
    Fallback Off
}
