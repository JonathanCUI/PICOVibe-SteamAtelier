Shader "SteamWorkshop/BlastJetV5" { Properties { _Density("Jet density",Float)=20 } SubShader {Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+20"} Pass { Blend One OneMinusSrcAlpha ZWrite Off Cull Front ZTest Always HLSLPROGRAM

 #pragma vertex vert
 #pragma fragment frag
 #pragma target 3.5
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
 struct A{float4 positionOS:POSITION;};struct V{float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;};
 V vert(A a){V o;o.world=TransformObjectToWorld(a.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);return o;}
 float hash(float3 p){return frac(sin(dot(p,float3(127.1,311.7,74.7)))*43758.5453);}
 float noise(float3 p){float3 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(lerp(hash(i),hash(i+float3(1,0,0)),f.x),lerp(hash(i+float3(0,1,0)),hash(i+float3(1,1,0)),f.x),f.y),lerp(lerp(hash(i+float3(0,0,1)),hash(i+float3(1,0,1)),f.x),lerp(hash(i+float3(0,1,1)),hash(i+1),f.x),f.y),f.z);}
 bool rayBounds(V i,out float3 ro,out float3 rd,out float start,out float end){float3 ray=normalize(i.world-_WorldSpaceCameraPos);ro=TransformWorldToObject(_WorldSpaceCameraPos);rd=mul((float3x3)unity_WorldToObject,ray);float3 inv=rcp(rd+1e-7),a=(-.5-ro)*inv,b=(.5-ro)*inv,lo=min(a,b),hi=max(a,b);start=max(0,max(lo.x,max(lo.y,lo.z)));end=min(hi.x,min(hi.y,hi.z));float2 uv=i.positionCS.xy/_ScaledScreenParams.xy;float depth=SampleSceneDepth(uv);
 #if !UNITY_REVERSED_Z
 depth=lerp(UNITY_NEAR_CLIP_VALUE,1,depth);
 #endif
 float3 opaque=ComputeWorldSpacePosition(uv,depth,UNITY_MATRIX_I_VP);end=min(end,dot(opaque-_WorldSpaceCameraPos,ray));return end>start;}

 CBUFFER_START(UnityPerMaterial) float _Density; CBUFFER_END
 half4 frag(V i):SV_Target{float3 ro,rd;float start,end;if(!rayBounds(i,ro,rd,start,end))discard;float dt=(end-start)/32;float alpha=0;[loop]for(int k=0;k<32;k++){float3 p=ro+rd*(start+(k+.5)*dt);float h=p.y+.5;float radius=.22+h*.21;float r=length(p.xz);float edge=saturate((radius-r)/.09)*saturate((1-h)*8);float n=noise(p*14+float3(0,-_Time.y*5,0))*.65+noise(p*29+float3(0,-_Time.y*9,0))*.35;float a=1-exp(-edge*(.28+n*.72)*_Density*dt);alpha+=(1-alpha)*a;}return half4(float3(.90,.95,1)*alpha,alpha*.92);}
 ENDHLSL } } }
