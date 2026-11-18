// Danger zones on the ground: drawn above the ground but behind anything standing on it.
//
// The zone has no depth test of its own, so small bumps in the terrain never cut holes in it. Instead each
// pixel is compared with what the camera already sees there: if that is nearer than the zone by more than
// _Slack (a unit, a wall, a tree), the zone is hidden; if it is about level with the zone (the ground), it shows.
Shader "BoneSquadKiting/TelegraphOverlay"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _Slack ("Ground slack (world units)", Float) = 0.35
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Slack;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float eyeDepth : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(positionWS);
                o.eyeDepth = -TransformWorldToView(positionWS).z;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 uv = i.positionCS.xy / _ScaledScreenParams.xy;
                float raw = SampleSceneDepth(uv);
                float sceneDepth = unity_OrthoParams.w == 0
                    ? LinearEyeDepth(raw, _ZBufferParams)
                    : LinearDepthToEyeDepth(raw);
                clip(sceneDepth + _Slack - i.eyeDepth);   // something stands in front of the zone here
                return _Color;
            }
            ENDHLSL
        }
    }
}
