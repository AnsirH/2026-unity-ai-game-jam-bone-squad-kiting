#ifndef TWIN_SWORD_IMPACT_INCLUDED
#define TWIN_SWORD_IMPACT_INCLUDED

// The rectangular slash mesh spans three metres across and ten metres forward.
float3 ImpactPosition(float x, float along, float height)
{
    return float3(x / 3.0, along - 0.5, -height);
}

float Seed(float n) { return frac(sin(n * 127.1 + 4.17) * 4375.3); }

float ImpactLane(float group) { return 0.18 + group * 0.345; }
float ImpactStart(float lane) { return 0.62 * (1.0 - pow(1.0-lane, 0.606)); }
float ImpactAxis(float lane) { return 0.44 * sin(lane * 4.4 - 0.85); }

float DustNoise(float2 p)
{
    float2 c=floor(p), w=frac(p);
    w=w*w*(3-2*w);
    float n=dot(c,float2(1,73));
    return lerp(lerp(Seed(n),Seed(n+1),w.x),lerp(Seed(n+73),Seed(n+74),w.x),w.y);
}

// These vertices carry either a local stone, a billboard corner, a fractured blade face,
// or a point along a lightning branch. All share the same three timed ground contacts.
void ImpactVertex(Attributes v, float age, inout Varyings o)
{
    float id = v.part.y;
    float group = fmod(id,3);
    float lane = ImpactLane(group);
    float seed = Seed(id+1);
    float facing = fmod(floor(id/3),2) < 1 ? -1.0 : 1.0;
    float3 pos;
    float t = age - ImpactStart(lane);
    o.p = v.uv;
    o.impactLife = t;
    if (v.part.x < 4.5) // Ballistic, faceted stone
    {
        t = saturate((t-0.045*seed)/(0.53+0.16*seed));
        float side = facing*(0.12 + t*(0.45 + 0.68*seed));
        float rockSize=(0.095+0.17*Seed(id+26))*(0.85+0.15*group);
        float3 rock = v.positionOS * rockSize;
        float angle = seed*6.28+t*(2.1+seed*4);
        rock.xz=mul(float2x2(cos(angle),-sin(angle),sin(angle),cos(angle)),rock.xz);
        float height=4*t*(1-t)*(0.5+1.0*seed)+rock.y;
        pos=ImpactPosition(ImpactAxis(lane)+side+rock.x,lane+(t-0.2)*(Seed(id+13)-0.5)*0.18+rock.z/10,height);
        o.impactLife=t;
    }
    else if (v.part.x < 5.5) // Low rolling clouds, pushed to either side of the cut
    {
        t=saturate((t-0.035-seed*0.045)/(0.55+0.11*seed));
        float radius=(0.36+0.48*seed)*(0.60+t);
        float side=facing*(0.15+seed*0.22+t*(0.48+0.55*seed));
        pos=ImpactPosition(ImpactAxis(lane)+side,lane+(Seed(id+8)-0.5)*0.125+t*0.027,0.08+t*(0.46+seed*0.22));
        float3 centerWS=TransformObjectToWorld(pos);
        float3 cameraRight=UNITY_MATRIX_I_V._m00_m10_m20;
        float3 cameraUp=UNITY_MATRIX_I_V._m01_m11_m21;
        centerWS+=radius*(cameraRight*v.positionOS.x + cameraUp*v.positionOS.y*0.78);
        o.positionCS=TransformWorldToHClip(centerWS);
        o.impactLife=t;
        return;
    }
    else // Dark crests and the detached lightning around each crest
    {
        float peak = (group==1 ? 2.45 : 1.60+group*0.66);
        float rise = smoothstep(0,0.13,t)*(1-smoothstep(0.30,0.64,t));
        float lean = group==1 ? -0.68 : 0.66;
        if(v.part.x < 6.5)
        {
            float satellite=step(3,id);
            float h=v.positionOS.y;
            float scale=1-satellite*0.52;
            pos=ImpactPosition(ImpactAxis(lane)+satellite*facing*0.48 + (v.positionOS.x*0.56+lean*h*h)*scale*(0.24+0.76*rise),
                               lane + (v.positionOS.z*0.076 + h*h*0.095)*scale*(0.24+0.76*rise), h*peak*rise*scale);
        }
        else
        {
            float h=v.uv.x;
            float node=v.positionOS.x; // reproducible corners along a forked bolt
            float bend=Seed(node+id*17)-0.5;
            float span = sin(3.14159*h);
            float x=ImpactAxis(lane)+lean*h*h+facing*(0.03+(0.39+0.38*seed)*span)+bend*0.30*span;
            float z=lane+0.083*h*h+0.022*bend;
            x+=(v.uv.y-0.5)*(0.032+0.04*seed)*(0.4+span);
            pos=ImpactPosition(x,z,(0.05+h*peak)*rise*(0.75+0.25*seed));
        }
        o.impactLife=t;
    }
    o.positionCS=TransformObjectToHClip(pos);
}

half4 ImpactShade(Varyings i)
{
    float part=i.part.x, t=i.impactLife, seed=Seed(i.part.y+1);
    if(part < 4.5)
    {
        float a=smoothstep(0,0.06,t)*(1-smoothstep(0.74,1,t));
        half3 stone=lerp(half3(0.048,0.045,0.052),half3(0.46,0.40,0.30),i.p.x);
        stone+=_Steel.rgb*(0.05+0.22*i.p.x)*(1-t)*(1-t);
        return half4(stone*a,a);
    }
    if(part < 5.5)
    {
        float2 p=i.p*2-1;
        float2 uv=p*3.2+seed*54+float2(0,-t*0.9);
        float noise=0.68*DustNoise(uv)+0.32*DustNoise(uv*2.15+18.3);
        float mass=1-smoothstep(0.52,1.02,length(p)+(0.5-noise)*0.66);
        float a=mass*smoothstep(0,0.11,t)*(1-smoothstep(0.44,1,t))*0.76;
        half3 dust=lerp(half3(0.065,0.074,0.11),half3(0.36,0.31,0.25),saturate(noise*1.45+p.y*0.17));
        return half4(dust*a,a);
    }
    float a=smoothstep(0,0.075,t)*(1-smoothstep(0.34,0.62,t));
    if(part < 6.5)
    {
        float cut=abs(i.p.y-0.38-0.17*i.p.x);
        half3 body=half3(0.002,0.003,0.009)+_Spirit.rgb*(0.005+0.060*i.p.x);
        body+=_Steel.rgb*exp(-100*cut)*smoothstep(0.2,0.82,i.p.x);
        return half4(body*a,a);
    }
    half3 bolt=lerp(_Spirit.rgb*2,_Steel.rgb*4.0,1-abs(2*i.p.y-1));
    return half4(bolt*a,a*0.65);
}

float RockFracture(float2 p, float age)
{
    float stripe=floor(p.y*12);
    float root=(stripe+0.5)/12;
    float axis=0.32*sin(root*4.4-0.85);
    float side=fmod(stripe,2)<1 ? 1 : -1;
    float x=(p.x-axis)*side;
    float fractureZ=root+(0.05+0.025*Seed(stripe))*x+0.013*abs(frac(x*2.5+Seed(stripe))*2-1);
    float crack=abs(p.y-fractureZ);
    float fork=abs(p.y-fractureZ+max(x-0.45,0)*0.115);
    float width=0.0065*saturate(1-x);
    float branches=1-smoothstep(width,width+0.002,min(crack,fork));
    return branches*smoothstep(0.11,0.3,x)*(1-smoothstep(0.63,0.97,x))*smoothstep(0,0.10,age-ImpactStart(root));
}
#endif
