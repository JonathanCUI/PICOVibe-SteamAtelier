#ifndef CANYON_EARTH_INPUT
#define CANYON_EARTH_INPUT
#define InitializeStandardLitSurfaceData OriginalEarthSurface
#include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
#undef InitializeStandardLitSurfaceData
TEXTURE2D(_GravelMap); SAMPLER(sampler_GravelMap);
TEXTURE2D(_GravelNormal); SAMPLER(sampler_GravelNormal);
float EarthHash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
float EarthNoise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(EarthHash(i),EarthHash(i+float2(1,0)),f.x),lerp(EarthHash(i+float2(0,1)),EarthHash(i+1),f.x),f.y);}
void InitializeStandardLitSurfaceData(float2 uv,out SurfaceData s){OriginalEarthSurface(uv,s);float n=EarthNoise(uv*.33+17.2)*.65+EarthNoise(uv*.97-8.4)*.35;float blend=smoothstep(.32,.70,n);half3 g=SAMPLE_TEXTURE2D(_GravelMap,sampler_GravelMap,uv*.85).rgb;half3 ng=UnpackNormalScale(SAMPLE_TEXTURE2D(_GravelNormal,sampler_GravelNormal,uv*.85),.75);s.albedo=lerp(s.albedo,g*_BaseColor.rgb,blend*.9);half l=dot(s.albedo,half3(.2126,.7152,.0722));s.albedo=lerp(s.albedo,l*half3(1.12,.94,.77),.60)*(0.80+n*.35);s.normalTS=normalize(lerp(s.normalTS,ng,blend));s.smoothness=.09+blend*.05;s.metallic=0;}
#endif
