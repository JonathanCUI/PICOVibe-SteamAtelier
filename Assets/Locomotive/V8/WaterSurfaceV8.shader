Shader "SteamWorkshop/WaterSurfaceV8" {
Properties { _Heat("Boiling",Range(0,1))=0 }
SubShader {Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+20" "RenderType"="Transparent"}
Pass {Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
float _Heat;
struct A{float4 p:POSITION;float3 n:NORMAL;};struct V{float4 p:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;};
V vert(A a){V o;float3 p=a.p.xyz;float t=_Time.y;float wave=sin(p.x*9+t*1.2)*cos(p.z*17-t)*.0025+sin(p.x*19-p.z*9-t*1.7)*.0014;wave+=_Heat*(sin(p.x*22+t*4.5)*cos(p.z*23-t*3.1)*.009+sin(p.x*37-p.z*19-t*5)*.004);p.y+=wave;float dx=cos(p.x*9+t*1.2)*cos(p.z*17-t)*.0225+cos(p.x*19-p.z*9-t*1.7)*.0266;float dz=-sin(p.x*9+t*1.2)*sin(p.z*17-t)*.0425-cos(p.x*19-p.z*9-t*1.7)*.0126;dx+=_Heat*cos(p.x*22+t*4.5)*cos(p.z*23-t*3.1)*.198;dz-=_Heat*sin(p.x*22+t*4.5)*sin(p.z*23-t*3.1)*.207;o.normal=TransformObjectToWorldNormal(normalize(float3(-dx,1,-dz)));o.world=TransformObjectToWorld(p);o.p=TransformWorldToHClip(o.world);return o;}
half4 frag(V i):SV_Target {float3 N=normalize(i.normal),view=GetWorldSpaceNormalizeViewDir(i.world);if(dot(N,view)<0)N=-N;float fres=.02+.98*pow(1-saturate(dot(N,view)),5);float3 reflection=GlossyEnvironmentReflection(reflect(-view,N),i.world,.08,1);float2 uv=i.p.xy/_ScaledScreenParams.xy;float3 refraction=SampleSceneColor(uv+N.xz*.004);Light l=GetMainLight();float sparkle=pow(saturate(dot(N,normalize(view+l.direction))),180);float3 tint=float3(.13,.37,.35);float3 col=lerp(tint,reflection,saturate(.20+fres*.70))+l.color*sparkle*.7;col=lerp(col,refraction*.65+tint*.35,.12);return half4(col,.35+fres*.40);}
ENDHLSL }} }