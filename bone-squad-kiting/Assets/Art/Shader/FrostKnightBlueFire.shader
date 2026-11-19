Shader "BoneSquad/Frost Knight Blue Fire"
{
    Properties
    {
        _BurnPhase ("Burn Phase", Range(0,1)) = 1
        _FireTiling ("Flame cells across the area", Vector) = (8,6,0,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" }
        Pass
        {
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _BurnPhase;
                float4 _FireTiling;
            CBUFFER_END

            struct Varyings { float4 positionCS : SV_POSITION; float3 box : TEXCOORD0; };
            Varyings Vert(float3 p : POSITION)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(p);
                o.box = p;
                return o;
            }

            float Hash(float2 cell)
            {
                float3 q = frac(cell.xyx * float3(.1031, .1030, .0973));
                q += dot(q, q.yzx + 33.33);
                return frac((q.x + q.y) * q.z);
            }

            float Noise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                f = f*f*(3-2*f);
                return lerp(lerp(Hash(cell),Hash(cell+float2(1,0)),f.x),
                            lerp(Hash(cell+float2(0,1)),Hash(cell+1),f.x),f.y);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 ws = TransformObjectToWorld(i.box);
                float3 ray = normalize(TransformWorldToObjectDir(-GetWorldSpaceNormalizeViewDir(ws)));
                float3 face = lerp(-.5, .5, step(0, ray));
                float3 farT = abs(face-i.box) / max(abs(ray), .00001);
                float stride = min(farT.x,min(farT.y,farT.z)) / 20;
                float age = _BurnPhase;
                float envelope = smoothstep(0,.11,age) * (1-smoothstep(.59,1,age));
                float floorFlash = (1-smoothstep(.025,.23,age));
                float4 accum = 0;
                [unroll] for (int k=0; k<20; k++)
                {
                    float3 p = i.box + ray * ((k+.5)*stride);
                    float y = saturate(p.y+.5);
                    float2 uv = p.xz * _FireTiling.xy;
                    float2 bend = y * float2(sin(uv.y*2+age*10-y*6),cos(uv.x*2-age*8+y*5)) * .53;
                    float broad = Noise(uv + bend + float2(age*.7,0));
                    float detail = Noise(uv*2.4 + bend + float2(-age*2.5,y*4-age*5));
                    float height = envelope * (.018 + .98 * pow(saturate((broad*.81+detail*.19-.23)/.75),2.15));
                    float edge = smoothstep(0,.055,.5-max(abs(p.x),abs(p.z)));
                    float density = edge * (envelope*smoothstep(-.012,.072,height-y)
                        + floorFlash*(1-smoothstep(.012,.10,y)));
                    // Narrow, wavering tongues rise through the lower bed of flame.
                    float2 grid = uv * .83;
                    float2 id = floor(grid);
                    float seed = Hash(id + 13);
                    float2 center = .32 + .36 * float2(seed, Hash(id+29));
                    center.x += y * .24 * sin(age*21 + seed*19 - y*7);
                    float tip = envelope * (.24+.68*seed) * (.86+.14*sin(age*18+seed*27));
                    float level = saturate(y/max(tip,.001));
                    float radius = (.15+.07*seed)*(1-level);
                    float tongue = edge * envelope * (1-smoothstep(radius*.25,radius+.025,length(frac(grid)-center)))
                        * (1-smoothstep(tip-.07,tip,y));
                    density = .65*density + 1.7*tongue;
                    density += edge * envelope * (1-smoothstep(.03,.16,y)) * .65;
                    float core = saturate((height-y)*2.5 + floorFlash*.45);
                    float alpha = 1-exp(-density*stride*4.3);
                    float3 blue = lerp(float3(.002,.10,.48),float3(.012,.82,1.07),core);
                    blue += tongue*float3(.07,.95,1.42);
                    accum.rgb += (1-accum.a)*alpha*blue;
                    accum.a += (1-accum.a)*alpha;
                }
                return accum;
            }
            ENDHLSL
        }
    }
}
