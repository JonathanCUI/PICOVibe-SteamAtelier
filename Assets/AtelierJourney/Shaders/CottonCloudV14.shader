Shader "SteamJourney/CottonCloudV14"{
 Properties{_Noise("Noise volume",3D)="white"{} _Seed("Shape seed",Float)=0}
 SubShader{Tags{"Queue"="Transparent+20" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"}Pass{
 Blend One OneMinusSrcAlpha ZWrite Off Cull Front ZTest Always
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
 TEXTURE3D(_Noise);SAMPLER(sampler_Noise);float3 _MesaSunDir;float _MesaDay;
 CBUFFER_START(UnityPerMaterial)
 float _Seed;
 CBUFFER_END
 struct A{float4 p:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};struct V{float4 p:SV_POSITION;float3 wp:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO};
 V vert(A i){V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_TRANSFER_INSTANCE_ID(i,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.wp=TransformObjectToWorld(i.p.xyz);o.p=TransformWorldToHClip(o.wp);return o;}
 float ell(float3 p,float3 c,float3 r){return (length((p-c)/r)-1)*min(r.x,min(r.y,r.z));}
 float sm(float a,float b){float h=saturate(.5+.5*(b-a)/.040);return lerp(b,a,h)-.040*h*(1-h);}
 float density(float3 p){float phase=_Time.y*.11+_Seed;float drift=sin(phase)*.016;float s=ell(p,float3(-.27,-.10,0),float3(.16,.16,.20));s=sm(s,ell(p,float3(-.12,.02+drift,-.02),float3(.18,.23,.22)));s=sm(s,ell(p,float3(.10,.04-drift,.04),float3(.20,.25,.22)));s=sm(s,ell(p,float3(.28,-.08,0),float3(.15,.17,.19)));s=sm(s,ell(p,float3(-.07,-.18,.09),float3(.26,.11,.20)));s=sm(s,ell(p,float3(.15,-.15,-.09),float3(.23,.13,.20)));
 s=sm(s,ell(p,float3(-.28,.055,.10),float3(.10,.12,.13)));s=sm(s,ell(p,float3(-.15,.20,.04),float3(.12,.13,.14)));s=sm(s,ell(p,float3(.04,.245,.05),float3(.13,.12,.13)));s=sm(s,ell(p,float3(.25,.105,.08),float3(.12,.13,.14)));s=sm(s,ell(p,float3(-.07,-.01,.23),float3(.12,.14,.11)));s=sm(s,ell(p,float3(.16,-.045,.23),float3(.12,.12,.12)));s=sm(s,ell(p,float3(-.02,.075,-.22),float3(.15,.14,.11)));
 float3 uv=p*.8+float3(_Seed*.13,_Time.y*.003,0);float n=SAMPLE_TEXTURE3D_LOD(_Noise,sampler_Noise,uv,0).r*.70+SAMPLE_TEXTURE3D_LOD(_Noise,sampler_Noise,uv*1.9,0).r*.30;s+=(n-.5)*.14;return 1-smoothstep(-.025,.028,s);}
 half4 frag(V i):SV_Target{UNITY_SETUP_INSTANCE_ID(i);UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);float3 ro=TransformWorldToObject(GetCameraPositionWS()),rd=normalize(TransformWorldToObject(i.wp)-ro);float3 inv=1/(rd+1e-7),a=(-.5-ro)*inv,b=(.5-ro)*inv;float3 lo=min(a,b),hi=max(a,b);float t0=max(0,max(lo.x,max(lo.y,lo.z))),t1=min(hi.x,min(hi.y,hi.z));if(t1<=t0)discard;float depth=LinearEyeDepth(SampleSceneDepth(i.p.xy/_ScaledScreenParams.xy),_ZBufferParams);float dt=(t1-t0)/40;float3 lightOS=normalize(TransformWorldToObjectDir(_MesaSunDir));float4 sum=0;
 [loop]for(int k=0;k<40;k++){float3 p=ro+rd*(t0+(k+.5)*dt);float3 wp=TransformObjectToWorld(p);if(-TransformWorldToView(wp).z>depth)break;float d=density(p);if(d>.003){float shade=0;[unroll]for(int q=1;q<=3;q++)shade+=density(p+lightOS*(q*.07));float lit=exp(-shade*.59)*lerp(.70,1,smoothstep(-.25,.20,p.y));float3 dark=lerp(float3(.065,.11,.22),float3(.25,.33,.43),_MesaDay),bright=lerp(float3(.40,.53,.73),float3(1,1,.99),_MesaDay);float3 color=lerp(dark,bright,lit);float alpha=1-exp(-d*dt*20);sum.rgb+=(1-sum.a)*color*alpha;sum.a+=(1-sum.a)*alpha;if(sum.a>.985)break;}}return sum;}
 ENDHLSL
 }}
}
