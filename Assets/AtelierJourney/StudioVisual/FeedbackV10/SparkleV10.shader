Shader "SteamJourney/V10Sparkle"{SubShader{Tags{"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+45"}Pass{Blend One One ZWrite Off Cull Off
HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
struct A{float4 p:POSITION;float2 uv:TEXCOORD0;half4 c:COLOR;};struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;half4 c:COLOR;};V vert(A a){V o;o.p=TransformObjectToHClip(a.p.xyz);o.uv=a.uv*2-1;o.c=a.c;return o;}
half4 frag(V i):SV_Target{float2 q=abs(i.uv);float star=exp(-dot(q,q)*12)+.55*exp(-q.x*35-q.y*3)+.55*exp(-q.y*35-q.x*3);return half4(i.c.rgb*i.c.a*star*2,0);}
ENDHLSL}}}
