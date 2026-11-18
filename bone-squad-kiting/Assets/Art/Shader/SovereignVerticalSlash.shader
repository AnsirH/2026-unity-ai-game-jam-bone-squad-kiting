Shader "BoneSquad/Vertical Sovereign Slash"
{
    Properties
    {
        [HDR] _Edge ("Haki edge", Color) = (0.025,0.40,1.35,1)
        _Windup ("Telegraph before strike (s)", Float) = 1.15
        _StrikeDuration ("Strike and wake (s)", Float) = 0.4
        _Elapsed ("Strike time override (s, -1 = loop)", Float) = -1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" }
        Pass
        {
            Cull Off
            ZWrite Off
            Blend One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Edge;
                float _Windup, _StrikeDuration, _Elapsed;
            CBUFFER_END

            struct Input { float3 pos : POSITION; float2 uv : TEXCOORD0; float2 part : TEXCOORD1; };
            struct Output { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float2 part : TEXCOORD1; float age : TEXCOORD2; };

            float Hash(float n) { return frac(sin(n*43.7 + 7.3)*17531.17); }
            float Jag(float x)
            {
                float n = floor(x);
                return lerp(Hash(n),Hash(n+1),frac(x))*2-1;
            }
            float Head(float age)
            {
                // A brief gathering motion, then a single decisive stroke.
                float t=saturate((age-0.095)/0.59);
                return (1-pow(1-t,1.45))*8.47+0.34;
            }
            float Envelope(float age)
            {
                return smoothstep(0,0.17,age)*(1-smoothstep(0.69,0.96,age));
            }
            float3 Crest(float u,float depth,float age)
            {
                float open=smoothstep(0.0,0.17,age);
                float arch=pow(max(0,sin(3.14159*u)),0.80);
                float reach = 1.65*arch;
                float thickness = arch*(1.92+0.74*(1-u));
                float edge = Jag(u*17+2)*0.10+Jag(u*42)*0.035;
                float trail = thickness*depth*(1+0.25*Jag(u*11+1));
                float x=arch*(0.10-0.18*depth);
                float y=0.095+u*(3.25-0.32*u)+arch*0.42 - depth*arch*0.44;
                return float3(x*(0.30+0.70*open),0.06+(y-0.06)*open,Head(age)+(reach+arch*edge-trail)*open);
            }
            float3 WorldUprightPosition(float3 local)
            {
                const float3 up = float3(0,1,0);
                float3 z = TransformObjectToWorldDir(float3(0,0,1),false);
                float3 x = TransformObjectToWorldDir(float3(1,0,0),false);
                float3 y = TransformObjectToWorldDir(up,false);
                float3 flat = float3(z.x,0,z.z);
                if (dot(flat,flat) < 1e-8*max(dot(z,z),1e-8))
                    flat = cross(x,up);
                float3 forward = SafeNormalize(flat);
                float3 right = cross(up,forward);
                return TransformObjectToWorld(float3(0,0,0))
                     + right*(local.x*length(x)) + up*(local.y*length(y)) + forward*(local.z*length(z));
            }

            Output Vert(Input v)
            {
                Output o;
                float span=max(_StrikeDuration,0.01);
                float elapsed=_Elapsed<0 ? fmod(_Time.y,max(0,_Windup)+span)-max(0,_Windup) : _Elapsed;
                float a=elapsed/span;
                o.age=a; o.uv=v.uv; o.part=v.part;
                float3 p;
                float3 ribbonOffsetWS=0;
                if(v.part.x<0.5)
                    p=float3(v.pos.x*3,0.035,(v.pos.y+0.5)*10);
                else if(v.part.x<1.5)
                    p=Crest(v.uv.x,v.uv.y,a);
                else
                {
                    // Ragged discharges share the moving crest. Broken forks flare and retract with it.
                    float id=v.part.y;
                    float t=v.uv.x;
                    bool release=id>=6;
                    float birth=release ? 0.64+0.06*Hash(id+7) : 0.11+0.38*Hash(id+7);
                    float lifetime=release ? 0.23 : 0.26+0.22*Hash(id+17);
                    float life=saturate((a-birth)/lifetime);
                    float u=lerp(0.12+Hash(id)*0.65,0.18+Hash(id+8)*0.76,t);
                    float3 root=Crest(u,0.30,a);
                    float j=Jag(t*8+id*13);
                    float side=fmod(id,2)<1?-1:1;
                    float spread=sin(3.14159*t);
                    p=root+float3(side*(0.18+spread*(0.38+0.38*Hash(id+2))+j*0.13),
                                  spread*0.32+j*0.22,-t*(1.05+1.60*Hash(id+3))-j*0.28)*smoothstep(0,0.25,life);
                    if (release)
                    {
                        // Three short sparks shear off the lip as the blade reaches the far edge.
                        float anchor=0.22+0.56*Hash(id+9);
                        float3 launch=Crest(anchor,0.035,0.67);
                        float3 tangent=float3(side*(0.65+0.20*side),side*0.48,-0.79);
                        p=launch + tangent*(life*1.15+t*0.48) + float3(0.10*j,0.035*j,0);
                    }
                    float width=(0.023+0.060*Hash(id+11))*(0.18+spread)*(1-life)*(release ? 1.40 : 1.0);
                    ribbonOffsetWS=UNITY_MATRIX_I_V._m01_m11_m21*(v.uv.y-0.5)*width*2;
                    o.part.y=life;
                }
                if (v.part.x >= 0.5)
                {
                    // Bury half the upright crest. Keep its leading edge aligned with the warning.
                    float midspan = lerp(0.06, 1.56, smoothstep(0.0,0.17,a));
                    float nose = Head(a) + 1.58 * smoothstep(0,0.17,a);
                    p.xy = float2(p.x, p.y - midspan) * 3;
                    p.z = nose + (p.z - nose) * 3;
                }
                o.pos=TransformWorldToHClip(WorldUprightPosition(p)+ribbonOffsetWS);
                return o;
            }
            half4 Frag(Output i):SV_Target
            {
                float a=i.age;
                clip(min(a,1-a));
                float2 uv=i.uv;
                if(i.part.x<0.5)
                {
                    float z=uv.y*10;
                    float lag=Head(a)-z;
                    float x=uv.x*2-1 + 0.22*sin(uv.y*5)-0.14;
                    float wake=smoothstep(-0.22,0.33,lag)*exp(-0.59*max(lag,0))*(1-smoothstep(0.66,1,a));
                    float weathering = 0.6*Jag(z*3.1 + x*1.7) + 0.4*Jag(z*8.3 - x*2.6);
                    float taper=lerp(1.0,0.38,smoothstep(0.25,3.3,lag));
                    float distance=abs(x+0.08*Jag(z*2.1))-(0.29+0.15*Jag(z*1.8+3))*taper;
                    float feather=0.10+0.06*weathering+0.08*smoothstep(0.5,2.6,lag);
                    float ink=1-smoothstep(-feather,feather,distance);
                    float emission=exp(-abs(distance)/(0.052+0.026*(0.5+0.5*weathering)));
                    // Fade both light and ink to zero before the floor quad ends.
                    float endDistance=min(z,10.0-z);
                    float endFade=smoothstep(0.02,1.15+0.27*weathering,endDistance);
                    float sideFade=smoothstep(0,0.16,1-abs(uv.x*2-1));
                    float dissolve=saturate(0.76+0.22*weathering-0.12*max(lag,0));
                    float strength=wake*endFade*sideFade*dissolve*smoothstep(0,0.12,a);
                    return half4(_Edge.rgb*emission*strength,ink*strength*0.91);
                }
                if(i.part.x>1.5)
                {
                    float life=i.part.y;
                    float spark=smoothstep(0,0.12,life)*(1-smoothstep(0.63,1,life));
                    return half4(_Edge.rgb*spark*(2.1+2.7*(1-abs(uv.y*2-1))),spark);
                }
                float u=uv.x, d=uv.y;
                float fade=Envelope(a);
                // The blade is ink with a narrow lit cutting edge. Break the trailing edge into dry brush.
                float feather=0.78-0.78*smoothstep(0.66,0.97,a)+0.13*Jag(u*41+1.4)+0.09*Jag(u*77);
                float opacity=(1-smoothstep(feather,feather+0.055,d))*smoothstep(0,0.06,sin(3.14159*u));
                float barb=abs(d-0.49-0.21*sin(u*13)-0.06*Jag(u*28));
                opacity *= 1-0.76*(1-smoothstep(0.005,0.029,barb))*smoothstep(0.49,0.70,d);
                float edge=1-smoothstep(0.015,0.092,d);
                float vein=abs(d-0.27-0.10*Jag(u*18+2)-0.13*sin(u*11));
                float pulse=exp(-85*vein)*(0.2+0.8*smoothstep(0.1,0.62,u));
                float streak=exp(-52*abs(d-0.62-0.12*sin(u*11)))*smoothstep(0.12,0.65,u);
                half3 ink=half3(0.001,0.002,0.006);
                half3 color=ink+_Edge.rgb*(2.9*edge+0.9*pulse+0.28*streak+0.12*exp(-7*d));
                color+=half3(0.18,0.52,0.91)*exp(-110*d);
                // A brief travelling catchlight describes the sweep without adding a second blade.
                float glint=exp(-15*abs(u-saturate((a-0.12)/0.46)));
                float lip=exp(-22*d);
                color+=glint*lip*half3(0.90,2.70,4.2)*smoothstep(0.08,0.19,a)*(1-smoothstep(0.56,0.69,a));
                return half4(color*opacity*fade,opacity*fade);
            }
            ENDHLSL
        }
    }
}