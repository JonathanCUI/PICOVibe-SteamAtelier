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
 #include "CloudDensityV16.hlsl"
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
