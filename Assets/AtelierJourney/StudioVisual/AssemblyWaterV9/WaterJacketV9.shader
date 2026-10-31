Shader "SteamJourney/V9WaterJacket" {
SubShader {Tags {"RenderPipeline"="UniversalPipeline" "DisableBatching"="True" "Queue"="Transparent+5" "RenderType"="Transparent"}
Pass {Blend SrcAlpha OneMinusSrcAlpha ZWrite Off ZTest Always Cull Front
HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "../StudioArrivalClip.hlsl"
struct A{float4 p:POSITION;};struct V{float4 p:SV_POSITION;float3 world:TEXCOORD0;};
V vert(A a){V o;o.world=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.world);return o;}
half4 frag(V i):SV_Target {
 float3 ray=normalize(i.world-_WorldSpaceCameraPos),ro=TransformWorldToObject(_WorldSpaceCameraPos),rd=mul((float3x3)unity_WorldToObject,ray);
 float3 safe=sign(rd+1e-7)*max(abs(rd),1e-6),lo=float3(-.5,-.5,-.5),hi=float3(.5,.3625,.5);
 float3 a=(lo-ro)/safe,b=(hi-ro)/safe,entry=min(a,b),leave=max(a,b);float nearT=max(max(entry.x,entry.y),entry.z),farT=min(min(leave.x,leave.y),leave.z);nearT=max(nearT,0);
 float2 uv=i.p.xy/_ScaledScreenParams.xy;float raw=SampleSceneDepth(uv);float eye=LinearEyeDepth(raw,_ZBufferParams);float rayForward=dot(ray,-UNITY_MATRIX_V[2].xyz);farT=min(farT,eye/max(rayForward,.001));StudioArrivalRay(ray,nearT,farT);if(farT<=nearT)return 0;
 float ds=(farT-nearT)/24,thickness=0,weightedDepth=0;
 [unroll]for(int k=0;k<24;k++){float3 p=ro+rd*(nearT+(k+.5)*ds);float y=2.14+p.y*1.6,z=p.z*1.5;float outer=step(abs(z),.75)*step(1.43,y)*step(y,2.18+sqrt(max(0,.5625-z*z)));float inner=step(abs(z),.53)*step(y,2.05+sqrt(max(0,.2809-z*z)));float inside=outer*(1-inner)*step(z,0);thickness+=inside*ds;weightedDepth+=inside*ds*saturate((2.72-y)/1.29);}
 if(thickness<.0001)return 0;float depth=weightedDepth/thickness;float alpha=1-exp(-thickness*4.5);float3 shallow=float3(.07,.32,.37),deep=float3(.008,.055,.105);float3 color=lerp(shallow,deep,saturate(depth*1.25+alpha*.25));
 color+=float3(.015,.035,.035)*saturate(1-depth);return half4(color,alpha*.78);
}
ENDHLSL }} }
