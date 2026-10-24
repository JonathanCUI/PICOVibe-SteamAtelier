Shader "SteamWorkshop/CylinderSteamV7" {
 Properties { _BaseColor("Steam tint",Color)=(.94,.96,1,1) _Density("Density",Float)=3 _Bounds("Half bounds",Vector)=(.5,.5,.5,0) _Floor("Lower normalized height",Float)=-1 _NoiseScale("Noise",Float)=9 }
 SubShader { Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" }
 Pass { Blend SrcAlpha OneMinusSrcAlpha ZWrite Off ZTest Always Cull Front
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
 CBUFFER_START(UnityPerMaterial)
 float4 _BaseColor; float4 _Bounds;float _Density,_Floor,_NoiseScale;
 CBUFFER_END
 struct A {float4 positionOS:POSITION;};struct V {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;};
 V vert(A a){V o;o.world=TransformObjectToWorld(a.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);return o;}
 float hash(float3 p){return frac(sin(dot(p,float3(127.1,311.7,74.7)))*43758.5453);}
 float noise(float3 p){float3 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(lerp(hash(i),hash(i+float3(1,0,0)),f.x),lerp(hash(i+float3(0,1,0)),hash(i+float3(1,1,0)),f.x),f.y),lerp(lerp(hash(i+float3(0,0,1)),hash(i+float3(1,0,1)),f.x),lerp(hash(i+float3(0,1,1)),hash(i+1),f.x),f.y),f.z);}
 half4 frag(V i):SV_Target {
 float3 ray=normalize(i.world-_WorldSpaceCameraPos);float3 ro=TransformWorldToObject(_WorldSpaceCameraPos);float3 rd=mul((float3x3)unity_WorldToObject,ray);float3 inv=rcp(rd+1e-7);float3 t0=(-_Bounds.xyz-ro)*inv,t1=(_Bounds.xyz-ro)*inv;float3 lo=min(t0,t1),hi=max(t0,t1);float start=max(0,max(lo.x,max(lo.y,lo.z))),end=min(hi.x,min(hi.y,hi.z));
 float2 uv=i.positionCS.xy/_ScaledScreenParams.xy;float depth=SampleSceneDepth(uv);
 #if !UNITY_REVERSED_Z
 depth=lerp(UNITY_NEAR_CLIP_VALUE,1,depth);
 #endif
 float3 opaque=ComputeWorldSpacePosition(uv,depth,UNITY_MATRIX_I_VP);end=min(end,dot(opaque-_WorldSpaceCameraPos,ray));if(end<=start)discard;
 float dt=(end-start)/28;float alpha=0;float lum=0;[loop]for(int j=0;j<28;j++){float3 p=ro+rd*(start+(j+.5)*dt);float3 q=p/_Bounds.xyz;float radial=length(q.yz);float edge=saturate((1-radial)*12)*saturate((1-abs(q.x))*14)*saturate((q.y-_Floor)*16);float3 wp=TransformObjectToWorld(p);float3 flow=wp*_NoiseScale+float3(_Time.y*.35,-_Time.y*.47,_Time.y*.13);float n=noise(flow)*.67+noise(flow*2.13)*.33;float d=edge*saturate((n-.35)*3.1);float a=1-exp(-d*_Density*dt);lum+=(1-alpha)*a*(.78+n*.22);alpha+=(1-alpha)*a;}
 return half4(_BaseColor.rgb*(alpha>0?lum/alpha:1),alpha*_BaseColor.a);
 }
 ENDHLSL
 }
 }
}
