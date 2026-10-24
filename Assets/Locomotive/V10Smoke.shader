Shader "SteamWorkshop/SmokeV10" {SubShader {Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent"} Pass {Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
struct A{float4 p:POSITION;float4 c:COLOR;float2 uv:TEXCOORD0;};struct V{float4 p:SV_POSITION;float4 c:COLOR;float2 uv:TEXCOORD0;};V vert(A a){V o;o.p=TransformObjectToHClip(a.p.xyz);o.c=a.c;o.uv=a.uv;return o;}
float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
half4 frag(V i):SV_Target{float2 p=i.uv*2-1;float n=noise(i.uv*4+_Time.y*.045)*.55+noise(i.uv*9-_Time.y*.03)*.3+noise(i.uv*19)*.15;float shape=saturate(1-dot(p,p)+(n-.5)*.60);float alpha=pow(shape,1.7)*(.30+.9*n)*i.c.a;float lighting=.65+shape*.45+n*.35;return half4(i.c.rgb*lighting,alpha);}
ENDHLSL}}}
