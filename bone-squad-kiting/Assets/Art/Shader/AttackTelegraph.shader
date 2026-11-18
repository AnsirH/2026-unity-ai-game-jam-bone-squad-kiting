Shader "BoneSquad/Attack Telegraph"
{
    Properties
    {
        _FillColor ("Fill", Color) = (1, 0.12, 0.24, 0.48)
        _EdgeColor ("Rim Color", Color) = (1, 0.47, 0.39, 0.92)
        [ToggleUI] _LethalTelegraph ("Lethal Telegraph", Float) = 0
        _LethalFill ("Lethal Fill", Color) = (0.32, 0.016, 0.052, 0.86)
        _LethalRim ("Lethal Rim", Color) = (0.76, 0.095, 0.19, 0.98)
        _CellSize ("Honeycomb Cell Width", Range(0.2, 2)) = 0.76
        _Honeycomb ("Honeycomb Strength", Range(0, 1)) = 0.28
        _RimWidth ("Rim Width", Range(0.02, 0.6)) = 0.21
        _FresnelStrength ("Fresnel Strength", Range(0, 4)) = 1.8
        [Enum(Rectangle,0,Disc,1,Donut,2)] _Shape ("Shape", Float) = 0
        _InnerRadius ("Donut Inner / Outer Radius", Range(0.05,0.95)) = 0.5
        [HideInInspector] _Opacity ("Cast Opacity", Float) = 1
        _Progress ("Progress", Range(0,1)) = 0.5
        [Toggle] _Loop ("Loop Preview", Float) = 0
        _Duration ("Reveal Duration (s)", Float) = 0.25
        _AfterFade ("Time Hidden After Fade (s)", Float) = 0
        _FadeDuration ("Fade Duration (s)", Float) = 0.4
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _FillColor, _EdgeColor, _LethalFill, _LethalRim;
                float _LethalTelegraph;
                float _CellSize, _Honeycomb, _RimWidth, _FresnelStrength;
                float _Shape, _InnerRadius, _Progress, _Opacity, _Loop, _Duration, _FadeDuration, _AfterFade;
            CBUFFER_END

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 localXY : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float2 sizeWS : TEXCOORD2;
            };

            Varyings Vert(float3 positionOS : POSITION)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(positionOS);
                output.localXY = positionOS.xy;
                output.positionWS = TransformObjectToWorld(positionOS);
                output.sizeWS = float2(length(TransformObjectToWorldDir(float3(1, 0, 0), false)),
                                       length(TransformObjectToWorldDir(float3(0, 1, 0), false)));
                return output;
            }

            float HoneycombLines(float2 position)
            {
                // Two staggered grids supply the nearest hexagon center.
                float2 pitch = float2(1.0, 1.73205);
                float2 a = (frac(position / pitch + 0.5) - 0.5) * pitch;
                float2 b = (frac(position / pitch) - 0.5) * pitch;
                float2 cell = abs(dot(a, a) < dot(b, b) ? a : b);
                float wall = 0.5 - max(cell.x, dot(cell, float2(0.5, 0.866025)));
                float blur = max(fwidth(wall), 0.035);
                return 1.0 - smoothstep(0.035, 0.085 + blur, wall);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // The preview uses a unit XY quad. Its lower edge is the rectangle origin.
                float distance01 = _Shape > 0.5 ? length(input.localXY) * 2.0 : input.localXY.y + 0.5;
                float progress = saturate(_Progress);
                float opacity = _Opacity;
                if (_Loop > 0.5)
                {
                    float duration = max(_Duration, 0.01);
                    float fadeStart = duration + 0.5;
                    float fadeDuration = max(_FadeDuration, 0.01);
                    float fadeEnd = fadeStart + fadeDuration;
                    float elapsed = fmod(_Time.y, fadeEnd + max(_AfterFade, 0.0));
                    progress = saturate(elapsed / duration);
                    opacity = 1.0 - smoothstep(fadeStart, fadeEnd, elapsed);
                }
                bool donut = _Shape > 1.5;
                float startRadius = donut ? _InnerRadius : 0.0;
                float extent = donut ? 1.0 - startRadius : progress;
                if (donut)
                    opacity *= smoothstep(0.0, 1.0, progress);
                float inside = startRadius + extent - distance01;
                float boundary = donut ? min(inside, distance01 - startRadius) : inside;
                float pixel = max(fwidth(distance01), 0.0001);
                float coverage = saturate(boundary / pixel + 0.5) * step(0.0001, progress);
                float2 localMeters = input.localXY * input.sizeWS;

                float2 rectMargin = float2((0.5 - abs(input.localXY.x)) * input.sizeWS.x,
                                          min(distance01, inside) * input.sizeWS.y);
                float margin = _Shape > 0.5 ? boundary * input.sizeWS.x * 0.5 : min(rectMargin.x, rectMargin.y);
                float rim = 1.0 - smoothstep(0.0, _RimWidth + fwidth(margin), margin);
                float3 normalWS = TransformObjectToWorldNormal(float3(0, 0, -1));
                float3 viewDir = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float fresnel = pow(1.0 - saturate(abs(dot(normalWS, viewDir))), 2.0);
                half4 fillColor = lerp(_FillColor, _LethalFill, _LethalTelegraph);
                half4 rimColor = lerp(_EdgeColor, _LethalRim, _LethalTelegraph);
                rimColor.rgb *= 1.0 + fresnel * _FresnelStrength;
                // Fade across the current footprint so even a partially grown shape has a clear center.
                float inradius = _Shape > 0.5 ? extent * input.sizeWS.x * (donut ? 0.25 : 0.5)
                                              : min(input.sizeWS.x, progress * input.sizeWS.y) * 0.5;
                float patternFade = 1.0 - smoothstep(0.0, max(inradius * 0.75, 0.0001), margin);
                float cells = HoneycombLines(localMeters / _CellSize) * _Honeycomb * patternFade;
                half4 color = lerp(fillColor, rimColor, rim);
                color.rgb += 2.0 * rimColor.rgb * cells;
                color.a += (1.0 - color.a) * cells;
                color.a *= coverage * opacity * lerp(0.7, 1.0, _LethalTelegraph);
                return color;
            }
            ENDHLSL
        }
    }
}
