Shader "SteamJourney/CottonCloudV13" {
 Properties { _Noise("Noise volume",3D)="white"{} }
 SubShader { Tags{"Queue"="Transparent+20" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"} Pass {
  Blend One OneMinusSrcAlpha ZWrite Off Cull Front ZTest Always
  HLSLPROGRAM
  #pragma vertex vert
  #pragma fragment frag
  #pragma multi_compile_instancing
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
  TEXTURE3D(_Noise);SAMPLER(sampler_Noise);float3 _MesaSunDir;float _MesaDay;
  struct A {float4 positionOS:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};
  struct V {float4 positionCS:SV_POSITION;float3 posWS:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
  V vert(A i){V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.posWS=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.posWS);return o;}
  float ell(float3 p,float3 c,float3 r){return (length((p-c)/r)-1)*min(r.x,min(r.y,r.z));}
  float density(float3 p){float s=ell(p,float3(-.26,-.09,0),float3(.18,.18,.24));s=min(s,ell(p,float3(-.10,.06,0),float3(.19,.27,.25)));s=min(s,ell(p,float3(.10,.04,.05),float3(.22,.29,.28)));s=min(s,ell(p,float3(.28,-.07,0),float3(.16,.19,.21)));s=min(s,ell(p,float3(-.11,-.18,.12),float3(.24,.13,.23)));s=min(s,ell(p,float3(.13,-.15,-.12),float3(.25,.17,.23)));float n=SAMPLE_TEXTURE3D_LOD(_Noise,sampler_Noise,p*.8+float3(_Time.y*.004,0,0),0).r*.65+SAMPLE_TEXTURE3D_LOD(_Noise,sampler_Noise,p*1.9,0).r*.35;s+=(n-.5)*.30;return saturate((.024-s)*32)*smoothstep(-.40,-.28,p.y);}
  half4 frag(V i):SV_Target {UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);float3 ro=TransformWorldToObject(GetCameraPositionWS()),rd=normalize(TransformWorldToObject(i.posWS)-ro);float3 inv=1.0/(rd+1e-7);float3 a=(-.5-ro)*inv,b=(.5-ro)*inv;float3 nearV=min(a,b),farV=max(a,b);float t0=max(max(nearV.x,nearV.y),nearV.z),t1=min(min(farV.x,farV.y),farV.z);t0=max(0,t0);if(t1<=t0)discard;float2 uv=i.positionCS.xy/_ScaledScreenParams.xy;float depth=LinearEyeDepth(SampleSceneDepth(uv),_ZBufferParams);float dt=(t1-t0)/48;float3 lightOS=normalize(TransformWorldToObjectDir(_MesaSunDir));float4 sum=0;
   [loop]for(int k=0;k<48;k++){float t=t0+(k+.5)*dt;float3 p=ro+rd*t;float3 wp=TransformObjectToWorld(p);if(-TransformWorldToView(wp).z>depth)break;float d=density(p);if(d>.005){float shade=0;[unroll]for(int q=1;q<=5;q++)shade+=density(p+lightOS*(q*.065));float lit=exp(-shade*.48);float3 shadow=lerp(float3(.035,.065,.14),float3(.24,.31,.40),_MesaDay);float3 bright=lerp(float3(.34,.47,.70),float3(.97,.98,1),_MesaDay);float3 color=lerp(shadow,bright,lit);color+=pow(saturate(dot(-rd,lightOS)),6)*.10*_MesaDay;float alpha=1-exp(-d*dt*18);sum.rgb+=(1-sum.a)*color*alpha;sum.a+=(1-sum.a)*alpha;if(sum.a>.985)break;}}
   return sum;}
  ENDHLSL
 } }
}
