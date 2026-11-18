Shader "Bone Squad/Gorge River Mist"
{
    Properties
    {
        _MistColor ("Mist tint", Color) = (0.68, 0.76, 0.78, 1)
        _Density ("Density", Range(0, 0.6)) = 0.09
        _Thickness ("Layer thickness (m)", Float) = 3.4
        _SoftDistance ("Mesh intersection softness (m)", Float) = 1.5

    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "Water Mist"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _MistColor;
                float _Density, _Thickness, _SoftDistance;
            CBUFFER_END

            struct Attributes { float3 positionOS : POSITION; float2 channel : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 channel : TEXCOORD1;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.channel = v.channel;
                return o;
            }

            float Hash(float2 p)
            {
                float3 h = frac(p.xyx * float3(0.1031, 0.1030, 0.0973));
                h += dot(h, h.yzx + 33.33);
                return frac((h.x + h.y) * h.z);
            }

            float Noise(float2 p)
            {
                float2 cell = floor(p), f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1,0)), f.x),
                            lerp(Hash(cell + float2(0,1)), Hash(cell + 1), f.x), f.y);
            }

            float Wisps(float2 p, float t)
            {
                float banks = Noise(p * float2(0.102, 0.23) - float2(t * 0.042, 0.013 * t));
                float detail = Noise(p * float2(0.21, 0.43) + float2(13.4 - t * 0.067, 2.1) + banks);
                return smoothstep(0.16, 0.83, banks * 0.67 + detail * 0.33);
            }

            float MistDensity(float2 channel, float t)
            {
                float banks = 1.0 - smoothstep(9.0, 16.5, abs(channel.y));
                float reach = 1.0 - smoothstep(16.0, 36.0, abs(channel.x));
                return (0.10 + 0.90 * Wisps(channel, t)) * banks * reach;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float rawDepth = SampleSceneDepth(GetNormalizedScreenSpaceUV(i.positionCS));
                float sceneZ = IsPerspectiveProjection() ? LinearEyeDepth(rawDepth, _ZBufferParams) : LinearDepthToEyeDepth(rawDepth);
                float3 toEye = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float rayScale = max(0.001, TransformWorldToViewDir(toEye).z);
                float clearance = max(0.0, sceneZ + TransformWorldToView(i.positionWS).z) / rayScale;
                // Integrate only the air in front of the first opaque surface.
                float3 localRay = TransformWorldToObjectDir(toEye);
                float path = min(clearance, _Thickness / max(0.16, abs(localRay.y)));
                float contact = smoothstep(0, max(0.01, _SoftDistance), clearance);
                float2 span = localRay.xz * path;
                float density = 0.5 * (MistDensity(i.channel - span * 0.25, _Time.y)
                                     + MistDensity(i.channel - span * 0.75, _Time.y));
                float alpha = (1 - exp(-_Density * path * density)) * contact;
                return half4(_MistColor.rgb, saturate(alpha) * _MistColor.a);
            }
            ENDHLSL
        }
    }
}
