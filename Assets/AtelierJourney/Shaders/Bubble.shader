Shader "SteamJourney/Bubble" {SubShader{Tags{"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+25"}
Pass{Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
struct A{float4 p:POSITION;float4 c:COLOR;float2 uv:TEXCOORD0;};struct V{float4 p:SV_POSITION;float4 c:COLOR;float2 uv:TEXCOORD0;};
V vert(A a){V o;o.p=TransformObjectToHClip(a.p.xyz);o.c=a.c;o.uv=a.uv;return o;}
half4 frag(V i):SV_Target{float2 p=i.uv*2-1;float r=length(p);float ring=exp(-pow((r-.77)*12,2))*.55;float glint=exp(-dot(p-float2(-.33,.45),p-float2(-.33,.45))*80);float a=(ring+glint)*saturate((1-r)*12);return half4(i.c.rgb+glint*.4,i.c.a*a);}
ENDHLSL}}}
