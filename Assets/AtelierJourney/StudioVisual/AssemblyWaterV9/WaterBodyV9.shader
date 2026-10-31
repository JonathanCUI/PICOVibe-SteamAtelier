Shader "SteamJourney/V9WaterBody" {
Properties{_FlowTime("Time",Float)=0}
SubShader{Tags{"RenderPipeline"="UniversalPipeline" "DisableBatching"="True" "Queue"="Transparent+5" "RenderType"="Transparent"}
Pass{Blend SrcAlpha OneMinusSrcAlpha ZWrite Off ZTest Always Cull Front
HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma target 3.5
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "../StudioArrivalClip.hlsl"
struct A{float4 p:POSITION;};struct V{float4 p:SV_POSITION;float3 w:TEXCOORD0;};
V vert(A a){V o;o.w=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.w);return o;}
half4 frag(V i):SV_Target{
float3 ray=normalize(i.w-_WorldSpaceCameraPos),ro=TransformWorldToObject(_WorldSpaceCameraPos),rd=mul((float3x3)unity_WorldToObject,ray);float3 safe=sign(rd+1e-7)*max(abs(rd),1e-6);
float3 a=(float3(-.5,-.5,-.5)-ro)/safe,b=(float3(.5,.402985,.33)-ro)/safe;float3 en=min(a,b),ex=max(a,b);float nearT=max(0,max(max(en.x,en.y),en.z)),farT=min(min(ex.x,ex.y),ex.z);
float aa=dot(rd.yz,rd.yz),bb=dot(ro.yz,rd.yz),cc=dot(ro.yz,ro.yz)-.25,disc=bb*bb-aa*cc;if(disc<0)return 0;
if(aa>1e-8){nearT=max(nearT,(-bb-sqrt(disc))/aa);farT=min(farT,(-bb+sqrt(disc))/aa);}
float eye=LinearEyeDepth(SampleSceneDepth(i.p.xy/_ScaledScreenParams.xy),_ZBufferParams);farT=min(farT,eye/max(dot(ray,-UNITY_MATRIX_V[2].xyz),.001));StudioArrivalRay(ray,nearT,farT);float thickness=max(0,farT-nearT);if(thickness<.0001)return 0;
float midY=ro.y+rd.y*(nearT+farT)*.5;float depth=saturate((.402985-midY)/.82);float alpha=(1-exp(-thickness*1.85))*.72;
float3 color=lerp(float3(.075,.34,.38),float3(.008,.055,.105),saturate(depth*.95+thickness*.25));float grazing=pow(saturate(1-abs(ray.y)),3);color+=float3(.016,.035,.038)*grazing*(1-depth);return half4(color,alpha);}
ENDHLSL}}}
