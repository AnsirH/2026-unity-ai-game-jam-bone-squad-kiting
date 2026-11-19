Shader "Bone Squad/Forest Road Damp Ground"
{
    Properties
    {
        _WaterStrength ("Shallow water visibility", Range(0,1)) = 0.26
        _MudOpacity ("Wet earth darkening", Range(0,1)) = 0.34
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-15" "RenderType"="Transparent" }
        Pass
        {
            Name "Damp ground"
            Tags { "LightMode"="UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half _WaterStrength, _MudOpacity;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 patchUV : TEXCOORD0;
                float2 moisture : TEXCOORD1;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 patchUV : TEXCOORD1;
                float2 moisture : TEXCOORD2;
                float2 positionXZ : TEXCOORD3;
            };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.patchUV = v.patchUV;
                o.moisture = v.moisture;
                o.positionXZ = v.positionOS.xz;
                return o;
            }
            float Hash(float2 p)
            {
                float3 h = frac(p.xyx * float3(.1031,.11369,.13787));
                h += dot(h, h.yzx + 19.19);
                return frac((h.x + h.y) * h.z);
            }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f*f*(3-2*f);
                return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),
                            lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y);
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float2 p = i.positionXZ;
                float2 uv = i.patchUV;
                float coarse = Noise(p*1.45);
                float grit = Noise(p*8.0);
                uv.y += (Noise(p*.78)-.5)*.28*(1-abs(uv.y));
                float hollow = 1 - length(uv) + (coarse-.5)*.31 + (grit-.5)*.075;
                float edge = 1-smoothstep(.87,1,max(abs(i.patchUV.x),abs(i.patchUV.y)));
                half mud = smoothstep(.04,.54,hollow)*edge*_MudOpacity*lerp(.70,1.0,coarse);
                // Only the deepest part holds standing water. Positive relief breaks the shoreline.
                float ridges = Noise(p*2.4 + float2(coarse, 1.7));
                float basin = hollow - .52 - i.moisture.y*3.0 + (coarse-.5)*.46 + (ridges-.5)*.34;
                half water = smoothstep(-.06,.12,basin)*edge*i.moisture.x*_WaterStrength;
                float3 view = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float ripple = sin(dot(p,float2(2.7,18.0))+coarse*3.3)*.036;
                float canopy = Noise(p*1.35 + view.xz*.42 + coarse*.45);
                half sky = saturate(.13 + .60*smoothstep(.33,.68,canopy) + ripple + .12*(1-saturate(view.y)));
                half3 reflection = lerp(half3(.043,.029,.019),half3(.110,.084,.061),sky);
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    float4 shadowUV = ComputeScreenPos(TransformWorldToHClip(i.positionWS));
                #else
                    float4 shadowUV = TransformWorldToShadowCoord(i.positionWS);
                #endif
                reflection *= lerp(.45,1,GetMainLight(shadowUV).shadowAttenuation);
                half alpha = water + (1-water)*mud;
                return half4(reflection*water, alpha);
            }
            ENDHLSL
        }
    }
}
