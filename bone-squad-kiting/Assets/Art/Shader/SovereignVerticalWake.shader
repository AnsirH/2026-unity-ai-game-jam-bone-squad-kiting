Shader "BoneSquad/Vertical Sovereign Slash Wake"
{
    Properties
    {
        _TrailTime ("Tip Trail Duration (s)", Float) = 0.135
        _Windup ("Telegraph Before Strike (s)", Float) = 1.15
        _StrikeDuration ("Strike Duration (s)", Float) = 0.4
        _Elapsed ("Strike Time Override (s, -1 = Loop)", Float) = -1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+8" }
        Pass
        {
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _TrailTime, _Windup, _StrikeDuration, _Elapsed;
            CBUFFER_END

            struct Input { float3 position : POSITION; float2 uv : TEXCOORD0; float2 tip : TEXCOORD1; };
            struct Output { float4 position : SV_POSITION; float2 uv : TEXCOORD0; float tip : TEXCOORD1; float elapsed : TEXCOORD2; };

            float Hash2(float2 p)
            {
                float3 q = frac(p.xyx * float3(.1031,.1030,.0973));
                q += dot(q, q.yzx + 33.33);
                return frac((q.x + q.y) * q.z);
            }

            float Brush(float2 p)
            {
                float2 k = floor(p);
                float2 f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash2(k), Hash2(k + float2(1,0)), f.x),
                            lerp(Hash2(k + float2(0,1)), Hash2(k + 1), f.x), f.y);
            }

            float Jag(float z)
            {
                float i = floor(z);
                float a = frac(sin(i * 79.31 + 13.5) * 2345.2);
                float b = frac(sin((i + 1) * 79.31 + 13.5) * 2345.2);
                return lerp(a,b,frac(z)) * 2 - 1;
            }

            float3 TipPosition(float2 uv, float side, float elapsed)
            {
                float tail = uv.y;
                float sampleTime = clamp(elapsed - tail * _TrailTime, 0, .275);
                float age = sampleTime / .4;
                float head = (1 - pow(1 - saturate((age - .095) / .59), 1.45)) * 8.47 + .34;
                float opened = smoothstep(0, .17, age);
                float u = side > 0 ? .035 : .965;
                float arch = pow(sin(3.14159 * u), .8);
                float height = .095 + u * (3.25 - .32 * u) + arch * .42;
                float width = (.29 + .035 * side) * (1 - .76 * tail);
                float flutter = sin(tail * 5.6 + sampleTime * 17 + side) * tail;
                float verticalOffset = side * (.12 * tail + .16 * sin(tail * 3.14159) + .11 * flutter) + (uv.x - .5) * width;
                float mid = lerp(.06, 1.56, opened);
                float y = lerp(.06, height, opened) - mid - verticalOffset;
                float nose = head + 1.58 * opened;
                float z = head + 1.65 * arch * opened - .12;
                return float3((.1 * arch - .20 * tail + .07 * flutter) * 3, y * 3, nose + (z - nose) * 3);
            }

            Output Vert(Input v)
            {
                Output o;
                float span = max(.01, _StrikeDuration);
                float period = max(0, _Windup) + span;
                // The residue from the last strike continues through the start of the next warning.
                float elapsed = _Elapsed >= 0 ? _Elapsed : fmod(_Time.y + span, period);
                if (_Elapsed < 0 && _Time.y < _Windup) elapsed = -1;
                o.elapsed = elapsed * (.4 / span);
                o.uv = v.uv;
                o.tip = abs(v.tip.x);
                float3 p = o.tip > .5 ? TipPosition(v.uv, v.tip.x, o.elapsed) : v.position;
                float3 x = TransformObjectToWorldDir(float3(1,0,0), false);
                float3 y = TransformObjectToWorldDir(float3(0,1,0), false);
                float3 z = TransformObjectToWorldDir(float3(0,0,1), false);
                float3 up = float3(0,1,0);
                float3 flat = float3(z.x,0,z.z);
                if (dot(flat,flat) < 1e-8 * max(dot(z,z),1e-8)) flat = cross(x,up);
                float3 forward = SafeNormalize(flat);
                float3 world = TransformObjectToWorld(float3(0,0,0))
                             + cross(up,forward) * (p.x * length(x)) + up * (p.y * length(y)) + forward * (p.z * length(z));
                o.position = TransformWorldToHClip(world);
                return o;
            }

            float4 Frag(Output i) : SV_Target
            {
                if (i.tip > .5)
                {
                    float tail = i.uv.y;
                    float sampled = i.elapsed - tail * _TrailTime;
                    float emission = smoothstep(.025,.068,sampled) * (1 - smoothstep(.245,.286,sampled));
                    float strength = emission * pow(1 - tail,1.35);
                    float band = abs(i.uv.x * 2 - 1);
                    float core = exp(-9 * band * band);
                    float halo = 1 - smoothstep(.12,1,band);
                    return float4(strength * (core * float3(.055,.60,1.60) + halo * float3(.016,.114,.48)), strength * halo * .32);
                }

                float z = i.uv.y * 10;
                float x = (i.uv.x - .5) * 3;
                float across = abs(x) / 1.5;
                float bow = 1.45 * pow(saturate(1 - across * across),.80);
                float crossing = .4 * (.095 + .59 * (1 - pow(1 - saturate((z - .34 - bow) / 8.47),1 / 1.45)));
                float since = i.elapsed - crossing;
                float sweep = x + .13 * sin(z * .85 + x) + .085 * Jag(z * 1.6);
                float bristle = .75 * Brush(float2(sweep * 3.5,z * .91)) + .25 * Brush(float2(sweep * 17,z * 2.8));
                float fade = 1 - smoothstep(.27 + .20 * bristle,.70 + .32 * bristle,since);
                float drift = x + .12 * sin(z * .8 + x);
                float haze = .66 * Brush(float2(drift * 2.4,z * .72)) + .34 * Brush(float2(drift * 4.1 + .7,z * .43));
                float veil = 1 - smoothstep(.50 + .14 * haze,1,across);
                float ends = smoothstep(0,1.1 + .35 * haze,z) * (1 - smoothstep(7.90 + bow,min(10.,8.96 + bow),z));
                float settled = smoothstep(.02,.11,since) * veil * ends;
                float pigment = settled * fade * (.29 + .32 * haze);
                float tint = settled * (1 - smoothstep(.11,.65,since)) * (.048 + .17 * haze * haze);
                float age = since - .055;
                float wear = saturate((age-.12)/1.10);
                float edgeBlur=.022+.11*wear+max(fwidth(x),.008);
                float grain=Brush(float2(x*3.1,z*1.7));
                float center=x-.12 + .058*Jag(z*2.2)+.033*Jag(z*5.6)+.042*sin(z*1.3);
                float groove=1-smoothstep(.044,.085+edgeBlur,abs(center));
                float splinter=1-smoothstep(.016,.031+edgeBlur,abs(center+.17+.06*Jag(z*2.4)));
                splinter*=smoothstep(.20,.76,Brush(float2(z*1.6,4.7)));
                float mark=smoothstep(.005,.062,age)*(1-smoothstep(.16+.18*grain,1.02+.18*grain,age));
                mark*=smoothstep(0,.65+.28*grain,z)*(1-smoothstep(9.08,9.94,z));
                float ink=mark*(.89*groove+.34*splinter)*(1-.38*wear);
                float lip=exp(-abs(center-.09)/(.027+edgeBlur))*(.60+.4*grain);
                float light=mark*lip*(1-smoothstep(.03,.72,age));
                pigment=saturate(pigment+ink*(1-pigment));
                tint=tint*(1-.70*ink)+light*.82;
                return float4(pigment * float3(.005,.009,.020) + tint * float3(.022,.19,.61),pigment);
            }
            ENDHLSL
        }
    }
}
