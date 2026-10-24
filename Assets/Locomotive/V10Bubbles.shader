Shader "SteamWorkshop/BubblesV10" {SubShader {Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+25"} Pass {Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
struct A{float4 p:POSITION;float4 c:COLOR;float2 uv:TEXCOORD0;};struct V{float4 p:SV_POSITION;float4 c:COLOR;float2 uv:TEXCOORD0;};V vert(A a){V o;o.p=TransformObjectToHClip(a.p.xyz);o.c=a.c;o.uv=a.uv;return o;}
half4 frag(V i):SV_Target{float2 p=i.uv*2-1;float r=length(p);clip(.98-r);float rim=smoothstep(.45,.87,r)*(1-smoothstep(.88,.98,r));float spec=exp(-dot(p-float2(-.32,.38),p-float2(-.32,.38))*38);float alpha=(rim*.62+spec*.72+.035)*i.c.a;return half4(i.c.rgb*(.5+rim*.5)+spec*.6,alpha);}
ENDHLSL}}}
