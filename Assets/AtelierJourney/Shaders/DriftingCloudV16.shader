Shader "SteamJourney/DriftingCloudV16" {
 Properties { _Noise("Density volume",3D)="white"{} _Seed("Shape seed",Float)=0 _Steam("Steam mode",Float)=0 _Opacity("Optical density",Float)=1 _Phase("Evolution",Float)=0 _Bench("Bench scale lighting",Float)=0 }
 SubShader { Tags {"Queue"="Transparent+20" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"} Pass {
 Blend One OneMinusSrcAlpha ZWrite Off Cull Front ZTest Always
 HLSLPROGRAM
 #pragma target 3.5
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
 TEXTURE3D(_Noise); SAMPLER(sampler_Noise); float3 _MesaSunDir; float _MesaDay;
 TEXTURE3D(_MesaFreeAir); SAMPLER(sampler_MesaFreeAir);
 float4x4 _MesaWorldToLocal;float3 _MesaFieldMin,_MesaFieldInvSize; float _MesaFieldEnabled;
 CBUFFER_START(UnityPerMaterial)
 float _Seed,_Steam,_Opacity,_Phase,_Bench;
 CBUFFER_END
 float freeAir(float3 wp){
  if(_Bench>.5||_Steam<.5||_MesaFieldEnabled<.5)return 1;
  float3 uv=(mul(_MesaWorldToLocal,float4(wp,1)).xyz-_MesaFieldMin)*_MesaFieldInvSize;
  if(any(uv<0)||any(uv>1))return 1;
  return smoothstep(.12,.96,SAMPLE_TEXTURE3D_LOD(_MesaFreeAir,sampler_MesaFreeAir,uv,0).r);
 }
 struct A {float4 p:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID};
 struct V {float4 p:SV_POSITION;float3 wp:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO};
 V vert(A a){V o;UNITY_SETUP_INSTANCE_ID(a);UNITY_TRANSFER_INSTANCE_ID(a,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.wp=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.wp);return o;}
 float noise3(float3 p){return SAMPLE_TEXTURE3D_LOD(_Noise,sampler_Noise,p,0).r;}
 float ell(float3 p,float3 c,float3 r){return (length((p-c)/r)-1)*min(r.x,min(r.y,r.z));}
 float merge(float a,float b){float h=saturate(.5+.5*(b-a)/.032);return lerp(b,a,h)-.032*h*(1-h);}
 float density(float3 p){
  float t=_Phase,seed=_Seed; float3 q=p;
  // Advect interior detail and roll lobes at different rates. This evolves the
  // silhouette even when the world transform and camera remain fixed.
  q.x+=sin(p.y*7+t*.73+seed)*.028;
  q.y+=sin(p.x*8-t*.91+seed)*.025+sin(p.z*11+t*.57)*.014;
  q.z+=cos(p.x*6+t*.62+seed)*.024;
  float s;
  if(_Steam>.5){
   s=ell(q,float3(-.10,-.06,0),float3(.24,.29,.25));
   s=merge(s,ell(q,float3(.14,.05,.02),float3(.23,.27,.22)));
   s=merge(s,ell(q,float3(-.11,.22+sin(t)*.025,.04),float3(.19,.18,.20)));
   s=merge(s,ell(q,float3(.23,-.13,-.05),float3(.15,.17,.19)));
   s=merge(s,ell(q,float3(-.03,.02,.24),float3(.19,.19,.14)));
  }else{
   // An asymmetric rising head and a thinning wind-stretched tail.
   float variant=sin(seed*1.7);
   s=ell(q,float3(-.24,-.10,.02),float3(.20,.19,.24));
   s=merge(s,ell(q,float3(-.14,.05+variant*.025,-.03),float3(.19,.25,.23)));
   s=merge(s,ell(q,float3(-.04,.20+sin(t*.67+seed)*.022,.02),float3(.15,.17,.20)));
   s=merge(s,ell(q,float3(.09,.04,-.04),float3(.19,.20,.22)));
   s=merge(s,ell(q,float3(-.04,-.15,.05),float3(.27,.13,.23)));
   s=merge(s,ell(q,float3(-.25,.08,.06),float3(.11,.13,.14)));
   s=merge(s,ell(q,float3(-.11,.03,.23),float3(.15,.15,.13)));
   s=merge(s,ell(q,float3(.07,-.04,.20),float3(.14,.13,.11)));
   s=merge(s,ell(q,float3(-.10,.02,-.24),float3(.17,.14,.12)));
   s=merge(s,ell(q,float3(-.22,.09,.22),float3(.11,.14,.13)));
   s=merge(s,ell(q,float3(-.03,.20,.20),float3(.10,.13,.12)));
   s=merge(s,ell(q,float3(.10,.075,.21),float3(.105,.12,.12)));
   s=merge(s,ell(q,float3(-.27,-.09,.22),float3(.12,.105,.11)));
   s=merge(s,ell(q,float3(-.23,.12,-.20),float3(.115,.13,.135)));
   s=merge(s,ell(q,float3(.04,.17,-.20),float3(.115,.12,.13)));
   s=merge(s,ell(q,float3(.23,-.08,.02),float3(.19,.070,.12)));
   s=merge(s,ell(q,float3(.32,.015+sin(t*.8+seed)*.03,-.04),float3(.14,.048,.085)));
  }
  float3 adv=q*.83+float3(-t*.048, t*.021,seed*.17+t*.014);
  float low=noise3(adv),fine=noise3(adv*2.71+float3(t*.018,-t*.023,0));
  float detail=noise3(adv*6.1-float3(t*.04,0,t*.027));
  s+=(low-.5)*.21+(fine-.5)*.065+(detail-.5)*.018;
  float body=1-smoothstep(-.032,.044,s);
  if(_Steam<.5){
   float tail=smoothstep(.08,.25,q.x)*(1-smoothstep(.40,.49,q.x));
   float strandY=q.y+.075-sin(q.x*18-t*.9+seed)*.035;
   float strandZ=q.z-sin(q.x*12-t*.55)*.07;
   float threads=exp(-strandY*strandY*100-strandZ*strandZ*65)*smoothstep(.43,.7,fine+detail*.18)*tail*.65;
   body=max(body*(1-tail*.65),threads);
  }
  float pad=1-smoothstep(.42,.498,max(abs(p.x),max(abs(p.y),abs(p.z))));
  return body*pad;
 }
 half4 frag(V i):SV_Target {
  UNITY_SETUP_INSTANCE_ID(i);UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
  float3 ro=TransformWorldToObject(GetCameraPositionWS()),rd=normalize(TransformWorldToObject(i.wp)-ro);
  float3 inv=1/(rd+1e-7),a=(-.5-ro)*inv,b=(.5-ro)*inv,lo=min(a,b),hi=max(a,b);
  float near=max(0,max(lo.x,max(lo.y,lo.z))),far=min(hi.x,min(hi.y,hi.z));if(far<=near)discard;
  float depth=LinearEyeDepth(SampleSceneDepth(i.p.xy/_ScaledScreenParams.xy),_ZBufferParams);
  int steps=_Steam>.5?28:56;float dt=(far-near)/steps;
  float3 lightOS=normalize(TransformWorldToObjectDir(_Bench>.5?normalize(float3(-.45,1,.4)):_MesaSunDir));float day=lerp(_MesaDay,1,_Bench);float4 sum=0;
  [loop]for(int k=0;k<steps;k++){
   float3 p=ro+rd*(near+(k+.5)*dt),wp=TransformObjectToWorld(p);float eye=-TransformWorldToView(wp).z;if(eye>depth)break;
   float d=density(p)*freeAir(wp);if(d>.003){
    float shade=density(p+lightOS*.055)*.48+density(p+lightOS*.12)*.63+density(p+lightOS*.23)*.77;
    float lit=exp(-shade*1.30);float top=smoothstep(-.34,.30,p.y);
    float3 dark=lerp(float3(.13,.20,.33),float3(.24,.30,.39),day);
    float3 bright=lerp(float3(.59,.72,.91),float3(1.03,1.01,.96),day);
    float3 color=lerp(dark,bright,saturate(lit*.88+top*.12));
    float fade=saturate((depth-eye)*2.5);
    float alpha=(1-exp(-d*dt*lerp(19,13,_Steam)*_Opacity))*fade;
    sum.rgb+=(1-sum.a)*color*alpha;sum.a+=(1-sum.a)*alpha;if(sum.a>.99)break;
   }
  }return sum;
 }
 ENDHLSL
 }}
}
