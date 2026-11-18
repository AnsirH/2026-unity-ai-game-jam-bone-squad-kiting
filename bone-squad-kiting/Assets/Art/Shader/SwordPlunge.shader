Shader "BoneSquad/Sword Plunge"
{
    Properties
    {
        [NoScaleOffset] _FractureMap ("Stone Fracture Field", 2D) = "white" {}
        _Elapsed ("Seconds Since Spawn", Float) = 0
        [HDR] _Energy ("Strike Blue", Color) = (0.035, 0.42, 1.5, 1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+12" "RenderType"="Transparent" }
        Pass
        {
            Cull Off
            ZWrite Off
            Blend One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_FractureMap);
            SAMPLER(sampler_FractureMap);

            CBUFFER_START(UnityPerMaterial)
                float _Elapsed;
                half4 _Energy;
            CBUFFER_END

            static const float Contact = 0.36;
            static const float Tau = 6.2831853;

            struct Vertex { float2 uv : TEXCOORD0; float2 layer : TEXCOORD1; float3 pos : POSITION; };
            struct Varyings { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float2 layer : TEXCOORD1; };

            float Hash(float n) { return frac(sin(n * 27.17 + 4.35) * 15736.9); }

            // The blade seats 66 ms after first contact.
            static const float SeatDelay = 0.066;

            bool IsStoneChip(float id) { return fmod(id, 3.0) < 0.5; }

            // Periodic angular noise keeps the brush seam closed around the full circle.
            float Fray(float turn, float count)
            {
                float index=frac(turn)*count;
                float cell=floor(index);
                return lerp(Hash(cell+2), Hash(fmod(cell+1,count)+2),frac(index));
            }

            Varyings Vert(Vertex v)
            {
                Varyings o;
                float age = _Elapsed - Contact;
                float3 p = v.pos;
                if (v.layer.x > 0.5)
                {
                    float3 viewOS = TransformWorldToObjectDir(UNITY_MATRIX_I_V._m02_m12_m22);
                    float2 right = float2(viewOS.z, -viewOS.x);
                    right = dot(right,right) > 0.0001 ? normalize(right) : float2(1,0);
                    if (v.layer.x < 1.5)
                    {
                        float fall = saturate(_Elapsed / Contact);
                        float tip = 3.8 * (1 - pow(fall,4.2)) - .52 * smoothstep(0,.022,age);
                        float settle = smoothstep(.062,.145,age);
                        tip += .12 * settle;
                        float stretch = 1 + .19 * pow(fall,5) * (1-smoothstep(-.014,.012,age));
                        p = float3(0, tip + v.uv.y * 3.65 * stretch, 0);
                        p.xz += right * ((v.uv.x-.5)*1.95 + v.uv.y * .055 * sin(65*age)*settle*exp(-9*max(0,age)));
                    }
                    else
                    {
                        float id = v.layer.y;
                        if (IsStoneChip(id))
                        {
                            float life = saturate((age-SeatDelay)/(0.33+0.14*Hash(id+2)));
                            float angle = Tau*(id+0.6*Hash(id))/18;
                            float2 direction = float2(cos(angle),sin(angle));
                            float radius = (0.28+0.4*Hash(id+8)) + life*(1.4+1.7*Hash(id+1));
                            float height = 4*life*(1-life)*(0.8+1.15*Hash(id+4));
                            float spin = id+life*(3+2*Hash(id+6));
                            float2 uv = (v.uv-.5)*float2(.62,.42)*(.7+.5*Hash(id));
                            float2 face = float2(uv.x*cos(spin)-uv.y*sin(spin),uv.x*sin(spin)+uv.y*cos(spin));
                            p.xz = direction*radius + right*face.x;
                            p.y = 0.10 + height + face.y;
                        }
                        else
                        {
                            float life = saturate(age / (0.29 + 0.19 * Hash(id+3)));
                            float angle = Tau * (id + 0.5 * Hash(id)) / 18;
                            float2 direction = float2(cos(angle),sin(angle));
                            float travel = 1 - pow(1-life,2.2);
                            float distance = travel * (0.95 + 2.2 * Hash(id+1));
                            float height = 4 * life * (1-life) * (.85 + 1.70 * Hash(id+9));
                            float taper = 1 - life;
                            p = float3(0, 0.08 + height, 0);
                            p.xz = direction * (distance + (v.uv.y-.5)*(.65+1.05*Hash(id+7))*taper);
                            p.xz += right*(v.uv.x-0.5)*(.20+.20*Hash(id+6))*taper;
                            p.y += (v.uv.y-0.5)*(.60+1.25*Hash(id+2))*taper;
                        }
                    }
                }
                o.pos=TransformObjectToHClip(p);
                o.uv=v.uv; o.layer=v.layer;
                return o;
            }

            half4 BrokenStone(float2 p, float r, float behind, float age)
            {
                // One connected fracture field: the spacing of its sites grows with impact radius.
                // R = distance to the joint (0..0.1 radii), G = bevel lighting, B = slab variation.
                float2 chipNoise = abs(frac(p.yx*float2(4.8,5.6) + .13) - .5) - .25;
                float2 warp = (.007 + .020*r)*chipNoise + .0017*sin(p.yx*79 + p*18);
                float3 fracture = SAMPLE_TEXTURE2D(_FractureMap,sampler_FractureMap,(p+warp)*.5+.5).rgb;
                float joint = fracture.r * .10;
                float aa = clamp(max(fwidth(p.x),fwidth(p.y))*.50,.0008,.006);
                float width = .0038 + .0018*saturate(r);
                float cut = 1-smoothstep(max(0,width-aa),width+aa,joint);
                float bevel = exp(-abs(joint-width-.003)/(.0023+.50*aa))*(.12+.88*fracture.g);
                float reach = .80+.09*sin(atan2(p.y,p.x)*3.0+.7)+.045*sin(p.x*19+p.y*12);
                float visible = (1-smoothstep(reach-.19,reach,r))*smoothstep(-.016,.028,behind)
                              *(1-smoothstep(.50,1.21,age));
                float shade = visible*(.075+.14*fracture.b+.74*cut);
                float heat = exp(-5.7*age)*(.20+.80*fracture.b);
                half3 lit = visible*bevel*(half3(.15,.125,.105)+_Energy.rgb*heat*.9);
                return half4(lit,saturate(shade));
            }

            half4 Ground(float2 uv, float age)
            {
                clip(age);
                float2 p=(uv-0.5)*2;
                float r=length(p), angle=atan2(p.y,p.x);
                float turn=angle/Tau+.5;
                float chop=Fray(turn,29), tooth=Fray(turn,79);
                // The flash marks first contact. The wave surges as the blade embeds.
                float t=saturate((age-.027)/.34);
                float front=.055+.89*(1-pow(1-t,2.7));
                float contour=front*(1+.022*(chop-.5)+.012*(tooth-.5));
                float behind=contour-r;
                float aa=max(fwidth(r),.002);
                float ringFade=1-smoothstep(.24,.57,age);
                float bandWidth=(.12+.17*chop+.065*tooth)*smoothstep(.02,.5,front);
                float ink=smoothstep(-aa,aa,behind)*(1-smoothstep(bandWidth*.68,bandWidth+.012,behind));
                float stroke=exp(-abs(behind)/(.006+aa))*(.19+1.47*smoothstep(.12,.76,chop));
                float vein=exp(-abs(behind-bandWidth*(.25+.25*tooth))/.009)*smoothstep(.39,.75,chop);
                float rupture=smoothstep(.008,.046,age)*ringFade;
                half4 stone=BrokenStone(p,r,behind,age);
                float impact=exp(-33*age);
                float flash=exp(-r/.085)*impact;
                float flare=exp(-r/.23)*pow(saturate(cos(angle*5+.8)),18)*impact*(1-smoothstep(.75,.98,r));
                half3 light=_Energy.rgb*(rupture*(2.15*stroke+.88*vein)+flash*3+flare*2.2);
                light+=half3(.82,1.6,2.1)*(flash+flare*.65+rupture*stroke*.30);
                // Briefly pick out the newly split stone faces under the seated blade.
                float seatPulse = smoothstep(SeatDelay-.012,SeatDelay,age)*exp(-27*max(0,age-SeatDelay));
                light += stone.rgb*seatPulse*3.2 + _Energy.rgb*seatPulse*exp(-r/.14)*.95;
                float waveAlpha=saturate(ink*.96*rupture+(flash+flare)*.7+.34*rupture*stroke);
                return half4(light+stone.rgb*(1-waveAlpha),waveAlpha+stone.a*(1-waveAlpha));
            }

            half4 Sword(float2 uv, float age)
            {
                float x=abs((uv.x-.5)*1.95), y=uv.y*3.65;
                float aa=max(fwidth(x),.009);
                float bladeWidth=lerp(.21,.30,saturate(y/2.48))*smoothstep(0,.50,y);
                float noise=lerp(Hash(floor(y*11)),Hash(floor(y*11)+1),frac(y*11));
                bladeWidth-=.035*noise*step(uv.x,.5);
                float blade=(1-smoothstep(bladeWidth-aa,bladeWidth+aa,x))*(1-smoothstep(2.48,2.52,y));
                float bevel=smoothstep(.79,.99,x/max(.012,bladeWidth));
                float ridge=exp(-abs((uv.x-.5)*1.95-.065*sin(y*3)-.043*noise)/.012);
                float guardDist=abs(y-(2.49+.19*pow(saturate(x/.76),1.4)));
                float guard=(1-smoothstep(.048,.072+aa,guardDist))*(1-smoothstep(.60,.78,x));
                float grip=(1-smoothstep(.060,.09,x))*smoothstep(2.46,2.51,y)*(1-smoothstep(3.19,3.25,y));
                float pommel=1-smoothstep(.095,.125,abs(y-3.29)+x*.8);
                float opacity=max(max(blade,guard),max(grip,pommel));
                float fade=smoothstep(0,.065,_Elapsed)*(1-smoothstep(.18,.58,age));
                float facet=uv.x>.5?.018:.003;
                float gleam=blade*(.004+facet+bevel*1.3+ridge*(.32+.32*noise)) + guard*(.37+.70*smoothstep(.025,.068,guardDist))
                            +grip*(.045+.15*pow(.5+.5*cos(y*55),3))+pommel*.75;
                half3 color=half3(.001,.002,.006)*opacity+_Energy.rgb*gleam+half3(.32,.58,.72)*blade*pow(bevel,5);
                return half4(color*fade,opacity*fade);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float age=_Elapsed-Contact;
                if(i.layer.x<.5) return Ground(i.uv,age);
                if(i.layer.x<1.5) return Sword(i.uv,age);
                clip(age);
                float u=i.uv.y;
                float id=i.layer.y;
                if (IsStoneChip(id))
                {
                    float life=(age-SeatDelay)/(.33+.14*Hash(id+2));
                    clip(life);
                    float2 q=i.uv*2-1;
                    float d=max(max(abs(q.x+.19*q.y)-.81,abs(q.y-.16*q.x)-.77),
                                max(q.x*.73+q.y-.94,-q.x*.86-q.y-.90));
                    float aa=max(fwidth(d),.008);
                    float alpha=(1-smoothstep(-aa,aa,d))*(1-smoothstep(.73,1,life));
                    float face=smoothstep(-aa,aa,q.y-.36*q.x+.09);
                    float lip=exp(-abs(d+.065)/.09)*smoothstep(-.4,.62,q.y-.3*q.x);
                    half3 rock=lerp(half3(.026,.023,.029),half3(.16,.145,.14),face);
                    return half4((rock + _Energy.rgb*lip*(.24+.85*(1-saturate(life))))*alpha,alpha);
                }
                float jag=lerp(Hash(floor(u*6)+id),Hash(floor(u*6)+id+1),frac(u*6));
                float profile=pow(saturate(sin(3.14159*u)),.85)*(.67+.33*jag);
                float across=(i.uv.x-.5)*2+.17*(jag-.5);
                float coverage=1-smoothstep(profile-.10,profile,abs(across));
                float life=age/(.29+.19*Hash(id+3));
                float alpha=coverage*smoothstep(0,.025,age)*(1-smoothstep(.45,1,life));
                float edge=exp(-abs(across-profile*.75)/.14);
                return half4((half3(.001,.003,.009)+_Energy.rgb*(.04+1.9*edge))*alpha,alpha);
            }
            ENDHLSL
        }
    }
}
