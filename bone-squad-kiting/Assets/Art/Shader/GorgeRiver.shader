Shader "Bone Squad/Gorge River"
{
    Properties
    {
        [NoScaleOffset] _Riverbed("Baked riverbed (R depth, G wake, B deflection)", 2D) = "white" {}
        [NoScaleOffset] _FlowDetail("Ripple slopes (RG), crests (B), noise (A)", 2D) = "gray" {}
        _DeepColor("Channel", Color) = (0.12, 0.24, 0.255, 1)
        _ShallowColor("Shallows", Color) = (0.31, 0.41, 0.36, 1)
        _SkyColor("Reflected sky", Color) = (0.55, 0.65, 0.67, 1)
        _FoamColor("Broken foam", Color) = (0.78, 0.82, 0.75, 1)
        _Speed("Current speed", Range(0, 3)) = 1
        _RippleStrength("Ripple strength", Range(0, 2)) = 1
        _FoamAmount("Foam coverage", Range(0, 1)) = 0.76
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "RenderType"="Opaque" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _DeepColor, _ShallowColor, _SkyColor, _FoamColor;
            float _Speed, _RippleStrength, _FoamAmount;
        CBUFFER_END
        TEXTURE2D(_FlowDetail);
        SAMPLER(sampler_FlowDetail);
        TEXTURE2D(_Riverbed);
        SAMPLER(sampler_Riverbed);

        struct Attributes
        {
            float3 positionOS : POSITION;
            float2 channel : TEXCOORD0;   // Along-stream and cross-stream distances in metres.
            float4 bed : TEXCOORD1;       // Riverbed lookup in XY, local bend slope in Z.
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float2 channel : TEXCOORD1;
            float4 bed : TEXCOORD2;
            half fog : TEXCOORD3;
        };
        Varyings Vert(Attributes v)
        {
            Varyings o;
            VertexPositionInputs pos = GetVertexPositionInputs(v.positionOS);
            o.positionCS = pos.positionCS;
            o.positionWS = pos.positionWS;
            o.channel = v.channel;
            o.bed = v.bed;
            o.fog = ComputeFogFactor(pos.positionCS.z);
            return o;
        }
        ENDHLSL

        Pass
        {
            Name "River"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            half4 FlowSample(float2 uv, float2 drift, float phase)
            {
                float cycle = floor(phase);
                return SAMPLE_TEXTURE2D(_FlowDetail, sampler_FlowDetail,
                    uv - frac(phase) * drift + cycle * float2(0.137, 0.083));
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float t = _Time.y * _Speed;
                float2 p = i.channel;
                half4 field = SAMPLE_TEXTURE2D(_FlowDetail, sampler_FlowDetail, p * 0.031);
                half3 bed = SAMPLE_TEXTURE2D(_Riverbed, sampler_Riverbed, i.bed.xy).rgb;
                float phase = t * 0.21 + field.a * 1.7;
                float2 drift = float2(0.68 + 0.30 * field.a, (bed.b * 2.0 - 1.0) * 0.31 + (field.g - 0.5) * 0.34);
                float2 uv = p * float2(0.078, 0.17) + (field.rg - 0.5) * 0.19;
                half4 a = FlowSample(uv, drift, phase);
                half4 b = FlowSample(uv + float2(0.37, 0.21), drift, phase + 0.5);
                half4 flow = lerp(b, a, 1.0 - abs(2.0 * frac(phase) - 1.0));
                half4 fine = SAMPLE_TEXTURE2D(_FlowDetail, sampler_FlowDetail,
                    p * float2(0.22, 0.32) - float2(t * 0.095, t * 0.011) + flow.rg * 0.10);

                float2 slope = ((flow.rg - 0.5) * 1.20 + (fine.rg - 0.5) * 0.46) * _RippleStrength;
                float2 along = normalize(float2(1.0, i.bed.z));
                float2 across = float2(-along.y, along.x);
                float2 tilt = slope.x * along + slope.y * across;
                half3 n = normalize(TransformObjectToWorldNormal(float3(tilt.x, 1.0, tilt.y)));
                half3 view = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float depth = max(bed.r * 6.0 - 2.0, 0.0);
                half3 body = lerp(_ShallowColor.rgb, _DeepColor.rgb, smoothstep(0.03, 1.68, depth));
                body *= lerp(0.84, 1.15, flow.a);
                half grazing = pow(1.0 - saturate(dot(n, view)), 3.0);
                half3 color = lerp(body, _SkyColor.rgb, 0.055 + 0.53 * grazing);
            #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                float4 shadowCoord = ComputeScreenPos(TransformWorldToHClip(i.positionWS));
            #else
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
            #endif
                Light sun = GetMainLight(shadowCoord);
                half3 h = SafeNormalize(sun.direction + view);
                half glint = pow(saturate(dot(n, h)), 84.0);
                half bank = 1.0 - smoothstep(0.05, 0.76, depth);
                half turbulence = saturate(max(bank, bed.g));
                half threads = smoothstep(0.53, 0.87, flow.b * 0.62 + fine.b * 0.38);
                half foam = saturate(turbulence * _FoamAmount * (threads + 0.13 * bank));
                color = lerp(color, _FoamColor.rgb, foam);
                color *= lerp(0.53, 1.0, sun.shadowAttenuation);
                color += sun.color * glint * 0.16 * sun.shadowAttenuation;
                return half4(MixFog(color, i.fog), 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Depth
            half4 Depth(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
