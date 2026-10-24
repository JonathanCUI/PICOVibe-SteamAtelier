Shader "SteamWorkshop/FireVolumeV5" {SubShader {Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+15"} Pass { Blend One OneMinusSrcAlpha ZWrite Off Cull Front ZTest Always HLSLPROGRAM

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

 half4 frag(V i):SV_Target{float3 ro,rd;float start,end;if(!rayBounds(i,ro,rd,start,end))discard;float dt=(end-start)/48;float4 sum=0;[loop]for(int k=0;k<48;k++){float3 p=ro+rd*(start+(k+.5)*dt);float h=p.y+.5;float3 q=p*float3(7,4,7)+float3(0,-_Time.y*2.8,0);float n=noise(q)*.7+noise(q*2.13)*.3;float sway=sin(h*5-_Time.y*3)*.035;float flame=0;[unroll]for(int f=0;f<5;f++){float angle=f*2.4;float2 center=float2(sin(angle)*.20,cos(angle)*.24);center+=float2(sway,cos(h*4+f+_Time.y*2)*.028);float tip=.64+.27*noise(float3(f*3,_Time.y*.8,0));float width=.19*(1-h*.72);float radial=length(p.xz-center)/max(.025,width);float body=saturate((1-radial+n*.6-h*.35)*3)*saturate((tip-h)*10);flame=max(flame,body);}float edge=saturate((.48-abs(p.x))*18)*saturate((.48-abs(p.z))*18);flame*=edge*saturate((n-.19)*3);float core=saturate(1-h*2.5)*saturate((n-.3)*3);float3 color=lerp(float3(1.8,.09,.003),float3(3.0,1.3,.10),core);float a=1-exp(-flame*dt*7);sum.rgb+=(1-sum.a)*a*color;sum.a+=(1-sum.a)*a;}return sum;}
 ENDHLSL } } }
