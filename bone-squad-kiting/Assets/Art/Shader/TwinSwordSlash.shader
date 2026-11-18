Shader "BoneSquad/Twin Sword Slash"
{
    Properties
    {
        [HDR] _Steel ("Steel Edge", Color) = (0.055, 0.49, 1, 1)
        [HDR] _Spirit ("Spirit Edge", Color) = (0.008, 0.12, 1, 1)
        _Windup ("Warning Time (s)", Float) = 0.75
        _StrikeDuration ("Strike and Dissolve (s)", Float) = 0.4
        _Elapsed ("Strike Time Override (s, -1 = Loop)", Float) = -1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" }
        Pass
        {
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Steel, _Spirit;
                float _Windup, _StrikeDuration, _Elapsed;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float2 part : TEXCOORD1;   // floor, two blade ribbons, or impact shards
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 p : TEXCOORD0;
                float2 part : TEXCOORD1;
                float age : TEXCOORD2;
                float impactLife : TEXCOORD3;
            };

            #include "TwinSwordImpact.hlsl"

            float StrokeAge()
            {
                float duration = max(_StrikeDuration, 0.01);
                float clock = _Elapsed < 0 ? fmod(_Time.y, max(0,_Windup) + duration) - max(0,_Windup) : _Elapsed;
                return clock / duration;
            }

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.age = StrokeAge();
                o.impactLife = 0;
                o.part = v.part;
                o.p = float2(v.positionOS.x * 2, v.positionOS.y + 0.5);
                float3 pos = v.positionOS;
                if (v.part.x >= 4)
                {
                    ImpactVertex(v, o.age, o);
                    return o;
                }
                if (v.part.x > 0.5 && v.part.x < 2.5)
                {
                    float side = v.part.x < 1.5 ? 1 : -1;
                    float phase = side > 0 ? o.age : (o.age-0.14)/0.86;
                    float travel = saturate(phase/0.62);
                    float tip = 1-pow(1-travel, 1.65);
                    float u = v.uv.x;
                    float vAcross = v.uv.y;
                    float along = max(0, tip - (1-u)*min(tip, 0.47));
                    float belly = pow(saturate(sin(3.14159*u)),0.80);
                    float bend = 0.55*sin(along*4.4-0.85);
                    float x = side*(bend + (vAcross-0.5)*belly*0.55 - (1-u)*(1-u)*0.36);
                    float height = sin(travel*2.8)*belly*(0.24+vAcross*(side>0?1.05:0.72));
                    pos = float3(0.5*x,along-0.5,-height);
                    o.p = v.uv;
                }
                else if (v.part.x > 2.5)
                {
                    float id = v.part.y;
                    float seed = frac(id*0.618034+0.19);
                    float angle = id*2.39996;
                    float life = saturate((o.age-0.48-seed*0.12)/(0.34+0.14*seed));
                    float spread = life*(1.0-0.4*life);
                    float3 direction = float3(cos(angle)*(0.26+0.64*seed),-0.10+sin(angle)*0.28,-(0.38+2.5*seed));
                    float3 center = float3(0,0.41,-0.07) + direction*spread;
                    center.z += life*life*(0.85+seed*0.8);
                    float width = (1-abs(v.uv.x*2-1))*(v.uv.y-0.5);
                    pos = center + direction*((v.uv.x-0.5)*(0.12+0.18*seed)) + float3(width*0.045,width*0.012,0);
                    o.p = v.uv;
                }
                o.positionCS = TransformObjectToHClip(pos);
                return o;
            }

            float Stroke(float d, float width)
            {
                float aa = max(fwidth(d), 0.0005);
                return 1 - smoothstep(width, width + aa, abs(d));
            }

            half3 Blade(float2 p, float age, float side, half3 tint)
            {
                // The spine runs diagonally across the lane. The crescent's center leads its tips.
                float travel = saturate(age / 0.55);
                float front = lerp(-0.035, 1.12, 1 - pow(1 - travel, 1.35));
                float x = p.x * side;
                float crest = front - 0.13*x*x + 0.05*x;
                float behind = crest - p.y;
                float wing = saturate(1 - x*x);
                float width = (0.013 + 0.011 * (0.5 + 0.5*x)) * wing;
                float sweep = Stroke(behind, width) * wing;
                float veil = exp(-max(behind, 0.0)*29) * smoothstep(-0.004, 0.006, behind) * wing;
                float axis = lerp(-0.52, 0.48, p.y) + 0.19*sin(p.y*4.3);
                float lag = front - p.y;
                float wake = smoothstep(-0.015, 0.045, lag) * exp(-2.3*max(lag, 0.0));
                float taper = saturate(4*p.y + 0.12) * saturate((1.04-p.y)*6);
                float slash = Stroke(x-axis, 0.021*taper) + 0.38*Stroke(x-axis+0.095+0.05*sin(p.y*6), 0.008*taper);
                float glow = exp(-13*abs(x-axis)) * wake;
                float fade = smoothstep(0, 0.065, age) * (1-smoothstep(0.53, 1, age));
                // A hairline catching the light breaks away from the blunt side of the arc.
                float split = Stroke(behind - (0.026 + 0.035*wing), 0.003*wing)
                            * wing * smoothstep(-0.55, 0.26, x);
                return fade * (tint*(2.25*sweep + 0.9*veil + 1.65*slash*wake*taper + 0.28*glow + split)
                             + half3(0.05,0.67,1.45)*1.85*Stroke(behind, width*0.16)*wing);
            }

            float Splinters(float2 p, float age)
            {
                float light = 0;
                [unroll] for (int k=0; k<8; k++)
                {
                    float side = (k%2 == 0) ? 1.0 : -1.0;
                    float birth = 0.12 + k * 0.049;
                    float life = saturate((age-birth) / 0.33);
                    float seed = frac(k*0.618+0.29);
                    float2 center = float2(side*(0.16+seed*0.24+life*0.42),
                                           0.09 + k*0.111 + life*0.085);
                    float2 delta = p-center;
                    float along = delta.y/(0.012+0.016*seed);
                    float across = delta.x - side*delta.y*2.8;
                    float taper = saturate(1-abs(along));
                    light += Stroke(across,0.012*taper)*taper*sin(3.14159*life);
                }
                return light;
            }

            float Burst(float2 q)
            {
                float2 a = abs(q);
                return exp(-5*a.x-72*a.y) + exp(-65*a.x-7*a.y)
                     + 0.6*exp(-38*abs(q.x-q.y*0.7)-10*a.y);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float age = i.age;
                clip(min(age, 1-age));

                if (i.part.x >= 4) return ImpactShade(i);
                if (i.part.x > 2.5)
                {
                    float seed = frac(i.part.y*0.618034+0.19);
                    float t = (age-0.48-seed*0.12)/(0.34+0.14*seed);
                    float fade = smoothstep(0,0.12,t)*(1-smoothstep(0.53,1,t));
                    float facet = smoothstep(0.41,0.53,i.p.y);
                    half3 spark = lerp(half3(0.001,0.005,0.02), 2.8*_Steel.rgb, facet);
                    return half4(spark*fade,fade);
                }
                if (i.part.x > 0.5)
                {
                    bool first = i.part.x < 1.5;
                    float t = first ? age : (age-0.14)/0.86;
                    float fade = smoothstep(0.015,0.12,t)*(1-smoothstep(0.55,0.85,t));
                    float torn = 0.08*abs(frac(i.p.x*8.5)*2-1)+0.035*sin(i.p.x*71);
                    float rim = smoothstep(0.76,0.98,i.p.y+torn);
                    float spine = Stroke(i.p.y-(0.34 + 0.13*sin(i.p.x*8)),0.016);
                    half3 tint = first ? _Steel.rgb : _Spirit.rgb;
                    half3 blade = half3(0.001,0.002,0.009) + tint*(4.0*rim + 0.80*spine);
                    blade += half3(0.28,0.82,1)*2.0*smoothstep(0.92,1,i.p.y);
                    return half4(blade*fade,fade);
                }

                float2 p = i.p;
                float front = saturate(1-pow(1-saturate(age/0.62),1.65));
                float tail = 1-smoothstep(0.66,1,age);
                float reached = smoothstep(-0.005,0.042,front-p.y)*tail;
                float spine = 0.32*sin(p.y*4.4-0.85);
                // Uneven margins give the black cut a torn silhouette above the red warning.
                float edgeSeed = p.y*29 + step(0,p.x-spine)*8.37;
                float tear = lerp(frac(sin(floor(edgeSeed)*53.7)*4385.3),
                                  frac(sin((floor(edgeSeed)+1)*53.7)*4385.3),frac(edgeSeed))*0.12 - 0.06;
                float gash = abs(p.x-spine) - (0.11+0.20*sin(p.y*3.14159)+tear);
                float ink = (1-smoothstep(0.0,0.085,gash))*reached;
                float electric = Stroke(gash,0.008)*reached;
                float undercut = smoothstep(-0.025,0.06,front-p.y)*(1-smoothstep(0.12,0.48,front-p.y))
                               * (1-smoothstep(0.61,0.99,abs(p.x)))*tail;
                float shadow = max(ink, undercut*0.94);
                half3 light = Blade(p,age,1,_Steel.rgb) + Blade(p,(age-0.14)/0.86,-1,_Spirit.rgb);
                light *= 1.0-0.75*ink;
                light += electric*lerp(_Spirit.rgb,_Steel.rgb,saturate(1.0-6.0*abs(front-p.y)))*2.6;
                light += Splinters(p, age)*_Steel.rgb*2.5;
                float2 crossPoint=float2(p.x-0.03,(p.y-0.40)*5.8);
                light += Burst(crossPoint)*_Steel.rgb*4.8*smoothstep(0.23,0.32,age)*(1-smoothstep(0.33,0.52,age));

                float2 hit = float2(p.x,(p.y-0.91)*6.67);
                float burstAge = saturate((age-0.48)/0.50);
                float pulse = smoothstep(0.0,0.12,burstAge)*(1-smoothstep(0.28,1,burstAge));
                float wave = Stroke(length(hit)-burstAge*0.8,0.019)*(0.75+0.25*sin(atan2(hit.y,hit.x)*7));
                light += pulse*(Burst(hit)*half3(1.6,3.5,6) + wave*_Steel.rgb*0.43);
                float fractures=RockFracture(p,age)*tail;
                shadow=max(shadow,fractures);
                light+=fractures*_Spirit.rgb*0.34;
                float bounds = smoothstep(0,0.045,1-abs(p.x))*smoothstep(0,0.012,p.y)*(1-smoothstep(0.985,1,p.y));
                return half4((light+half3(0.001,0.003,0.012)*shadow)*bounds,0.98*shadow*bounds);
            }
            ENDHLSL
        }
    }
}
