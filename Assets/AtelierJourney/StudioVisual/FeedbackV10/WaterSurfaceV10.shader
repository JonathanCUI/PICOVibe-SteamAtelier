Shader "SteamJourney/V10WaterSurface" {
Properties{_Heat("Boiling",Range(0,1))=0 _FlowTime("Time",Float)=0}
SubShader{Tags{"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+20" "RenderType"="Transparent"}
Pass{Blend One OneMinusSrcAlpha ZWrite Off Cull Off
HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma target 3.5
#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
#pragma multi_compile_fragment _ _SHADOWS_SOFT
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "../StudioArrivalClip.hlsl"
CBUFFER_START(UnityPerMaterial)
float _Heat,_FlowTime;
CBUFFER_END
float4 _WaterImpulses[8];float _WaterImpulseTime,_WaterWorldScale;float4x4 _WaterWorldToLocal;
float2 WaterScenePoint(float3 world){return mul(_WaterWorldToLocal,float4(world,1)).xz;}
float impactWave(float2 w){float h=0;[unroll]for(int k=0;k<8;k++){float4 q=_WaterImpulses[k];float age=_WaterImpulseTime-q.z;if(age>=0&&age<4){float d=distance(w,q.xy);float ring=d-age*1.7;h+=q.w*exp(-age*1.55)*exp(-ring*ring*5)*sin(d*22-age*20);h+=q.w*.25*exp(-age*2)*sin(age*9-d*3.5);}}return clamp(h,-.027,.027);}
float wave(float2 p){float t=_FlowTime;float warp=sin(dot(p,float2(2.7,4.1))+t*.37)*.38+sin(dot(p,float2(-5.2,3.3))-t*.28)*.22;
return (.003+_Heat*.006)*(sin(dot(p,float2(8.1,3.7))-t*1.43+warp)+.53*sin(dot(p,float2(-12.9,10.2))+t*1.91+warp*.6)+.28*sin(dot(p,float2(21.7,17.3))-t*2.57)+.16*sin(dot(p,float2(-32.4,26.1))+t*3.17));}
struct A{float4 p:POSITION;};struct V{float4 p:SV_POSITION;float3 w:TEXCOORD0;float2 local:TEXCOORD1;};
V vert(A a){V o;o.local=a.p.xz;float2 world=WaterScenePoint(TransformObjectToWorld(a.p.xyz));a.p.y+=wave(o.local)+impactWave(world);o.w=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.w);return o;}
half4 frag(V i):SV_Target{StudioArrivalClip(StudioArrivalPosition(i.w).y);float e=.003;float dx=(wave(i.local+float2(e,0))-wave(i.local-float2(e,0)))/(2*e);float dz=(wave(i.local+float2(0,e))-wave(i.local-float2(0,e)))/(2*e);
dx+=(impactWave(WaterScenePoint(i.w)+float2(e,0))-impactWave(WaterScenePoint(i.w)-float2(e,0)))/(2*e);dz+=(impactWave(WaterScenePoint(i.w)+float2(0,e))-impactWave(WaterScenePoint(i.w)-float2(0,e)))/(2*e);
float3 N=TransformObjectToWorldNormal(normalize(float3(-dx,1,-dz))),V=GetWorldSpaceNormalizeViewDir(i.w);if(dot(N,V)<0)N=-N;
float fres=.021+.979*pow(1-saturate(dot(N,V)),5);float3 reflection=GlossyEnvironmentReflection(reflect(-V,N),i.w,.10,1);
float2 uv=i.p.xy/_ScaledScreenParams.xy;float floorEye=LinearEyeDepth(SampleSceneDepth(uv),_ZBufferParams);float surfaceEye=-TransformWorldToView(i.w).z;float thickness=max(0,floorEye-surfaceEye)/max(.0001,_WaterWorldScale);float depth=1-exp(-thickness*1.6);
float3 tint=lerp(float3(.065,.27,.30),float3(.009,.062,.10),depth);Light l=GetMainLight(TransformWorldToShadowCoord(i.w));float sparkle=pow(saturate(dot(N,normalize(V+l.direction))),150)*l.shadowAttenuation;
float alpha=saturate(.13+depth*.24+fres*.66);float3 col=tint*alpha+reflection*(.055+fres*.72)+l.color*sparkle*.42;return half4(col,alpha);}
ENDHLSL}}}
