Shader "BoneSquad/Command Pulse"
{
    Properties
    {
        _Tint ("Command color", Color) = (0.25, 1, 0.52, 1)
        _Progress ("Progress", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+20" "RenderType"="Transparent" }
        Pass
        {
            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float _Progress;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 localUV : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.localUV = input.uv * 2 - 1;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float t = _Progress;
                float radius = length(input.localUV);
                float wave = 1 - (1 - t) * (1 - t);
                float rim = abs(radius - lerp(0.12, 0.92, wave)) - lerp(0.046, 0.012, t);

                // Fold the four quadrants onto one inward-pointing chevron.
                float2 p = abs(input.localUV);
                p = float2(max(p.x, p.y), min(p.x, p.y));
                float tip = lerp(0.47, 0.16, smoothstep(0, 0.62, t));
                float2 fromTip = p - float2(tip, 0);
                float2 arm = float2(0.17, 0.14);
                float chevron = length(fromTip - arm * saturate(dot(fromTip, arm) / dot(arm, arm))) - 0.041;
                float center = abs(input.localUV.x) + abs(input.localUV.y) - lerp(0.11, 0.022, wave);
                float shape = min(rim, min(chevron, center));
                float aa = max(fwidth(shape), 0.001);
                float fill = 1 - smoothstep(-aa, aa, shape);
                float outline = 1 - smoothstep(0.016-aa, 0.016+aa, shape);
                half3 ink = _Tint.rgb * 0.075;
                half3 face = lerp(_Tint.rgb, half3(1,1,0.90), saturate(1-t*3) * 0.70);
                return half4(lerp(ink, face, fill), outline * (1 - smoothstep(0.48, 1, t)));
            }
            ENDHLSL
        }
    }
}