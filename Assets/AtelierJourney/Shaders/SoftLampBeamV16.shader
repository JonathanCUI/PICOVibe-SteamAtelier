Shader "SteamJourney/SoftLampBeamV16" {
 Properties{_Strength("Density",Float)=.45 _Tint("Warm scattering",Color)=(1,.86,.66,1)}
 SubShader{Tags{"Queue"="Transparent+12" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"}Pass{
 Blend One OneMinusSrcAlpha ZWrite Off Cull Front ZTest Always
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
 CBUFFER_START(UnityPerMaterial)
 float _Strength;float4 _Tint;
 CBUFFER_END
 struct A{float4 p:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};struct V{float4 p:SV_POSITION;float3 wp:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO};
 V vert(A i){V o;UNITY_SETUP_INSTANCE_ID(i);UNITY_TRANSFER_INSTANCE_ID(i,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.wp=TransformObjectToWorld(i.p.xyz);o.p=TransformWorldToHClip(o.wp);return o;}
 half4 frag(V i):SV_Target{UNITY_SETUP_INSTANCE_ID(i);UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);float3 ro=TransformWorldToObject(GetCameraPositionWS()),rd=normalize(TransformWorldToObject(i.wp)-ro);float3 inv=1/(rd+1e-7),a=(-.5-ro)*inv,b=(.5-ro)*inv;float3 lo=min(a,b),hi=max(a,b);float t0=max(0,max(lo.x,max(lo.y,lo.z))),t1=min(hi.x,min(hi.y,hi.z));if(t1<=t0)discard;float depth=LinearEyeDepth(SampleSceneDepth(i.p.xy/_ScaledScreenParams.xy),_ZBufferParams);float dt=(t1-t0)/32;float4 sum=0;
 [loop]for(int k=0;k<32;k++){float3 p=ro+rd*(t0+(k+.5)*dt);float3 world=TransformObjectToWorld(p);float ed=-TransformWorldToView(world).z;if(ed>depth)break;
 float soft=saturate((depth-ed)*1.4);float u=p.z+.5;float r=length(p.xy)/(.025+u*.38);
 // Broad Gaussian scattering decays before it reaches the volume bounds.
 // Both axial and radial falloff start near the lamp: no solid cone or end cap.
 float d=exp(-r*r*4.8)*exp(-u*3.1)*smoothstep(0,.025,u)*(1-smoothstep(.55,1,u));
 d*=1-smoothstep(.40,.49,max(abs(p.x),abs(p.y)));
 float alpha=(1-exp(-d*dt*_Strength*2.3))*soft;float3 color=lerp(_Tint.rgb,float3(.94,.87,.75),u);
 sum.rgb+=(1-sum.a)*color*alpha;sum.a+=(1-sum.a)*alpha;}return sum;}
 ENDHLSL
 }}
}
