Shader "SteamWorkshop/IndustrialV8" {
Properties { _BaseColor("Color",Color)=(.12,.15,.16,1) _Metallic("Metal",Range(0,1))=.7 _Smoothness("Smooth",Range(0,1))=.5 _Finish("0 enamel 1 steel 2 cast",Float)=0 _BaseMap("Base",2D)="white"{} _Cutoff("Cutoff",Float)=.5 }
SubShader { Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque"}
Pass {Name "ForwardLit" Tags {"LightMode"="UniversalForward"}
HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
#pragma multi_compile_fragment _ _SHADOWS_SOFT
#pragma multi_compile_fog
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
CBUFFER_START(UnityPerMaterial)
float4 _BaseColor,_BaseMap_ST;float _Metallic,_Smoothness,_Finish,_Cutoff;
CBUFFER_END
struct A{float4 p:POSITION;float3 n:NORMAL;};struct V{float4 p:SV_POSITION;float3 w:TEXCOORD0;float3 n:TEXCOORD1;float3 o:TEXCOORD2;float4 sh:TEXCOORD3;half fog:TEXCOORD4;};
V vert(A a){V o;VertexPositionInputs p=GetVertexPositionInputs(a.p.xyz);o.p=p.positionCS;o.w=p.positionWS;o.o=a.p.xyz;o.n=TransformObjectToWorldNormal(a.n);o.sh=GetShadowCoord(p);o.fog=ComputeFogFactor(p.positionCS.z);return o;}
float hash(float3 p){p=frac(p*.1031);p+=dot(p,p.yzx+33.33);return frac((p.x+p.y)*p.z);}
float noise(float3 p){float3 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(lerp(hash(i),hash(i+float3(1,0,0)),f.x),lerp(hash(i+float3(0,1,0)),hash(i+float3(1,1,0)),f.x),f.y),lerp(lerp(hash(i+float3(0,0,1)),hash(i+float3(1,0,1)),f.x),lerp(hash(i+float3(0,1,1)),hash(i+float3(1,1,1)),f.x),f.y),f.z);}
half4 frag(V i):SV_Target {
 float3 N=normalize(i.n),view=GetWorldSpaceNormalizeViewDir(i.w);float grain=noise(i.o*420),broad=noise(i.o*32);float micro=lerp(.5,grain,saturate(1-length(fwidth(i.o))*200));
 float brushing=noise(i.o*float3(45,1100,80));float metalMode=step(.5,_Finish)*(1-step(1.5,_Finish)),castMode=step(1.5,_Finish);
 float finish=lerp(micro,brushing,metalMode);float variation=lerp(.035,.12,castMode);
 SurfaceData s=(SurfaceData)0;s.albedo=_BaseColor.rgb*(1+(broad-.5)*variation+(finish-.5)*.045);s.metallic=_Metallic;s.smoothness=saturate(_Smoothness+(finish-.5)*lerp(.045,.11,metalMode)+(broad-.5)*.04);s.normalTS=half3(0,0,1);s.occlusion=1;s.alpha=1;
 InputData d=(InputData)0;d.positionWS=i.w;d.normalWS=N;d.viewDirectionWS=view;d.shadowCoord=i.sh;d.fogCoord=i.fog;d.bakedGI=SampleSH(N);d.vertexLighting=VertexLighting(i.w,N);d.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.p);d.shadowMask=half4(1,1,1,1);
 half4 col=UniversalFragmentPBR(d,s);col.rgb=MixFog(col.rgb,i.fog);return col;
}
ENDHLSL }
UsePass "Universal Render Pipeline/Lit/ShadowCaster"
UsePass "Universal Render Pipeline/Lit/DepthOnly"
}}