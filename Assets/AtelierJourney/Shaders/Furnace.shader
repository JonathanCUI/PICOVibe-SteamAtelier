Shader "SteamJourney/Furnace" {
Properties {_Burn("Burn",Range(0,1))=0 _FlowTime("Time",Float)=0}
SubShader {Tags {"RenderPipeline"="UniversalPipeline" "DisableBatching"="True" "Queue"="Transparent+15"} Pass {Blend One OneMinusSrcAlpha ZWrite Off Cull Front ZTest Always
HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma target 3.5
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
float _Burn,_FlowTime;
struct A{float4 p:POSITION;};struct V{float4 p:SV_POSITION;float3 world:TEXCOORD0;};
V vert(A a){V o;o.world=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.world);return o;}
float hash(float3 p){return frac(sin(dot(p,float3(127.1,311.7,74.7)))*43758.5453);}
float noise(float3 p){float3 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(lerp(hash(i),hash(i+float3(1,0,0)),f.x),lerp(hash(i+float3(0,1,0)),hash(i+float3(1,1,0)),f.x),f.y),lerp(lerp(hash(i+float3(0,0,1)),hash(i+float3(1,0,1)),f.x),lerp(hash(i+float3(0,1,1)),hash(i+1),f.x),f.y),f.z);}
half4 frag(V i):SV_Target{
 if(_Burn<.01)discard;float3 ray=normalize(i.world-_WorldSpaceCameraPos),ro=TransformWorldToObject(_WorldSpaceCameraPos),rd=mul((float3x3)unity_WorldToObject,ray);
 float3 inv=rcp(rd+1e-7),a=(-.5-ro)*inv,b=(.5-ro)*inv,lo=min(a,b),hi=max(a,b);float start=max(0,max(lo.x,max(lo.y,lo.z))),end=min(hi.x,min(hi.y,hi.z));
 float depth=SampleSceneDepth(i.p.xy/_ScaledScreenParams.xy);
 #if !UNITY_REVERSED_Z
 depth=lerp(UNITY_NEAR_CLIP_VALUE,1,depth);
 #endif
 float3 opaque=ComputeWorldSpacePosition(i.p.xy/_ScaledScreenParams.xy,depth,UNITY_MATRIX_I_VP);end=min(end,dot(opaque-_WorldSpaceCameraPos,ray));if(end<=start)discard;
 float dt=(end-start)/40;float4 sum=0;
 [loop]for(int k=0;k<40;k++){
  float3 p=ro+rd*(start+(k+.5)*dt);float h=p.y+.5;float time=_FlowTime;
  float3 q=p*float3(11,7,12)+float3(0,-time*3.1,0);float coarse=noise(q);float fine=noise(q*2.1+float3(time*.5,0,0));float n=coarse*.68+fine*.32;
  float2 bend=float2(sin(h*9-time*4+p.z*7),cos(h*8-time*3+p.x*9))*(.025+h*.075);float flame=0;
  [unroll]for(int f=0;f<7;f++){
   float angle=f*2.399;float2 center=float2(sin(angle)*.26,cos(angle)*.27)+bend;
   float tip=.37+.52*noise(float3(f*4.9,time*1.5,3));float width=(.14+noise(float3(f*2,0,time*.9))*.05)*(1-h*.68);
   float radial=length(p.xz-center)/max(.018,width);
   float body=saturate((1-radial+(n-.48)*1.1)*3)*saturate((tip-h+(n-.5)*.23)*15);
   flame=max(flame,body);
  }
  float edge=saturate((.49-abs(p.x))*30)*saturate((.49-abs(p.z))*30);
  flame*=edge*smoothstep(.24,.63,n)*saturate((_Burn*1.2-h)*8);
  float core=saturate(1-h*3.1)*smoothstep(.27,.70,n);float3 color=lerp(float3(1.6,.105,.008),float3(2.4,1.75,.5),core);
  float alpha=1-exp(-flame*dt*8*_Burn);sum.rgb+=(1-sum.a)*alpha*color;sum.a+=(1-sum.a)*alpha;
 }
 return sum;
}
ENDHLSL}}}
