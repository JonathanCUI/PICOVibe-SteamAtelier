Shader "SteamJourney/V10CylinderMist" {
Properties{_Density("Density",Range(0,1))=0 _Front("Charging front",Range(0,1))=1 _FlowTime("Time",Float)=0 _Direction("Fill direction",Float)=1 _Exhaust("Exhaust",Float)=0}
SubShader{Tags{"RenderPipeline"="UniversalPipeline" "DisableBatching"="True" "Queue"="Transparent+8"}Pass{Blend One OneMinusSrcAlpha ZWrite Off Cull Front ZTest Always
HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma target 3.5
#pragma multi_compile_instancing
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
float _Density,_Front,_FlowTime,_Direction,_Exhaust;struct A{float4 p:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};struct V{float4 p:SV_POSITION;float3 world:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO};V vert(A a){V o;UNITY_SETUP_INSTANCE_ID(a);UNITY_TRANSFER_INSTANCE_ID(a,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.world=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.world);return o;}
float hash(float3 p){return frac(sin(dot(p,float3(12.989,78.23,37.719)))*43758.54);}
float noise(float3 p){float3 a=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(lerp(hash(a),hash(a+float3(1,0,0)),f.x),lerp(hash(a+float3(0,1,0)),hash(a+float3(1,1,0)),f.x),f.y),lerp(lerp(hash(a+float3(0,0,1)),hash(a+float3(1,0,1)),f.x),lerp(hash(a+float3(0,1,1)),hash(a+1),f.x),f.y),f.z);}
float mist(float3 p){float radial=smoothstep(0,.16,.25-dot(p.yz,p.yz));float cap=smoothstep(0,.10,.5-abs(p.x));
 float3 advect=p*float3(4.2,5.7,5.7)-float3(_FlowTime*_Direction*.14,_FlowTime*.08,0);float large=noise(advect*.7);float fine=noise(advect*1.8+large*1.2);float n=smoothstep(.26,.76,large*.66+fine*.34);
 float front=1-smoothstep(_Front-.12,_Front+.22,.5+p.x*_Direction);return radial*cap*(.06+n*.94)*lerp(.15+.85*front,1,_Exhaust);}
half4 frag(V i):SV_Target{UNITY_SETUP_INSTANCE_ID(i);UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);float3 ray=normalize(i.world-_WorldSpaceCameraPos),ro=TransformWorldToObject(_WorldSpaceCameraPos),rd=mul((float3x3)unity_WorldToObject,ray);float3 inv=rcp(rd+1e-7),a=(-.5-ro)*inv,b=(.5-ro)*inv,lo=min(a,b),hi=max(a,b);float start=max(0,max(lo.x,max(lo.y,lo.z))),end=min(hi.x,min(hi.y,hi.z));float depth=SampleSceneDepth(i.p.xy/_ScaledScreenParams.xy);
#if !UNITY_REVERSED_Z
depth=lerp(UNITY_NEAR_CLIP_VALUE,1,depth);
#endif
float3 opaque=ComputeWorldSpacePosition(i.p.xy/_ScaledScreenParams.xy,depth,UNITY_MATRIX_I_VP);end=min(end,dot(opaque-_WorldSpaceCameraPos,ray));if(end<=start||_Density<.002)discard;
Light key=GetMainLight();float3 ld=normalize(mul((float3x3)unity_WorldToObject,key.direction));float ds=(end-start)/20;float transmission=1;float3 color=0;
[loop]for(int k=0;k<20;k++){float d=start+(k+.5)*ds;float3 p=ro+rd*d;float local=mist(p);float opacity=1-exp(-local*_Density*7.0*ds);
 float shadow=exp(-(mist(p+ld*.09)+mist(p+ld*.22))*1.5);float vertical=saturate(p.y+.5);float phase=.45+.22*pow(saturate(dot(-ray,key.direction)),3);
 float3 light=float3(.14,.23,.28)+float3(.18,.20,.21)*vertical+key.color*shadow*phase;light=min(light,float3(.78,.84,.86));color+=transmission*opacity*light;transmission*=1-opacity;}
return half4(color,1-transmission);}
ENDHLSL}}}
