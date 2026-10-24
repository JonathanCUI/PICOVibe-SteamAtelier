Shader "SteamWorkshop/WaterBodyV8" {
SubShader {Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+5" "RenderType"="Transparent"}
Pass {Blend SrcAlpha OneMinusSrcAlpha ZWrite Off ZTest Always Cull Front
HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
struct A{float4 p:POSITION;};struct V{float4 p:SV_POSITION;float3 world:TEXCOORD0;};
V vert(A a){V o;o.world=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.world);return o;}
half4 frag(V i):SV_Target {
 float3 ray=normalize(i.world-_WorldSpaceCameraPos),ro=TransformWorldToObject(_WorldSpaceCameraPos),rd=mul((float3x3)unity_WorldToObject,ray);
 float3 safe=sign(rd+1e-7)*max(abs(rd),1e-6),lo=float3(-.5,-.5,-.5),hi=float3(.5,.323529,.5);
 float3 a=(lo-ro)/safe,b=(hi-ro)/safe,entry=min(a,b),leave=max(a,b);float nearT=max(max(entry.x,entry.y),entry.z),farT=min(min(leave.x,leave.y),leave.z);nearT=max(nearT,0);
 float2 uv=i.p.xy/_ScaledScreenParams.xy;float raw=SampleSceneDepth(uv);float eye=LinearEyeDepth(raw,_ZBufferParams);float rayForward=dot(ray,-UNITY_MATRIX_V[2].xyz);farT=min(farT,eye/max(rayForward,.001));if(farT<=nearT)return 0;
 float ds=(farT-nearT)/48,thickness=0,weightedDepth=0;
 [unroll]for(int k=0;k<48;k++){float3 p=ro+rd*(nearT+(k+.5)*ds);float inside=step(dot(p.yz,p.yz),.25);thickness+=inside*ds;weightedDepth+=inside*ds*saturate((.323529-p.y)/.823529);}
 if(thickness<.0001)return 0;float depth=weightedDepth/thickness;float alpha=1-exp(-thickness*4.5);float3 shallow=float3(.12,.38,.44),deep=float3(.025,.15,.23);float3 color=lerp(shallow,deep,saturate(depth*.85+alpha*.30));
 color+=float3(.015,.035,.035)*saturate(1-depth);return half4(color,alpha*.91);
}
ENDHLSL }} }