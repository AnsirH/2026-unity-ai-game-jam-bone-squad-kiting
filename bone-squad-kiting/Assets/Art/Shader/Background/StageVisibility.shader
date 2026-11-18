Shader "Bone Squad/Stage Visibility"
{
    Properties
    {
        [HideInInspector] _VisibilityReadMask ("Stencil read mask", Float) = 1
        [HideInInspector] _VisibilityWriteMask ("Stencil write mask", Float) = 0
        _Darkness ("Maximum darkness", Range(0, 1)) = 0.8
        _EdgeDrift ("Edge drift (UV)", Range(0, 0.005)) = 0.002
        [NoScaleOffset] _VisibilityMap ("Visibility (white = visible)", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+490" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Name "StageVisibility"
            Cull Off
            ZWrite Off
            ZTest Always
            // Multiply destination RGB by visibility, leaving camera alpha intact.
            Blend Zero SrcColor
            ColorMask RGB
            // Bit 0 skips root-shaded props. A draped mesh also marks bit 1 to avoid
            // multiplying the same pixel twice where slopes overlap in projection.
            Stencil { Ref 0 ReadMask [_VisibilityReadMask] WriteMask [_VisibilityWriteMask] Comp Equal Pass Invert }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #include "StageVisibility.hlsl"

            TEXTURE2D(_VisibilityMap);
            SAMPLER(sampler_VisibilityMap);

            CBUFFER_START(UnityPerMaterial)
                float _EdgeDrift;
                float _Darkness;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 phase : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                output.phase = float2(dot(input.uv, float2(8, 5)), dot(input.uv, float2(-4, 10)))
                             + frac(_Time.y * float2(0.068, -0.047));
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Smooth periodic drift across the baked banks. Interiors stay solid.
                float2 wave = 1.0 - 4.0 * abs(frac(input.phase) - 0.5);
                float2 drift = wave * (2.0 - abs(wave)) * _EdgeDrift;
                half visibility = SAMPLE_TEXTURE2D(_VisibilityMap, sampler_VisibilityMap, input.uv + drift).r;
                half brightness = StageBrightness(visibility, _Darkness);
                return half4(brightness.xxx, 1);
            }
            ENDHLSL
        }
    }
}