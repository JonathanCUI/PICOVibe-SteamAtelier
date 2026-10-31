Shader "SteamJourney/SocketGhost" {
Properties{_Wire("Structural contour",Float)=0 _ScanBounds("Local min, span, axis",Vector)=(0,1,0,0)}
SubShader {Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+24" "RenderType"="Transparent"}
 HLSLINCLUDE
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
 float _SocketLock;float _SocketAge;
 CBUFFER_START(UnityPerMaterial)
 float _Wire;float4 _ScanBounds;
 CBUFFER_END
 struct A{float4 p:POSITION;float3 n:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
 struct V{float4 p:SV_POSITION;float3 n:TEXCOORD0;float3 world:TEXCOORD1;float3 local:TEXCOORD2;UNITY_VERTEX_OUTPUT_STEREO};
 V vert(A a){V o;UNITY_SETUP_INSTANCE_ID(a);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.local=a.p.xyz;o.world=TransformObjectToWorld(a.p.xyz);o.n=TransformObjectToWorldNormal(a.n);o.p=TransformWorldToHClip(o.world);return o;}
 half4 Scan(V i){
  UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
  float localAxis=_ScanBounds.z<.5?i.local.x:(_ScanBounds.z<1.5?i.local.y:i.local.z);
  float coordinate=saturate((localAxis-_ScanBounds.x)/max(.01,_ScanBounds.y));
  // Full sweep in .85 seconds, readable hold, then smooth fade; 1.6 seconds total.
  float cycle=frac(_SocketAge/1.6)*1.6;
  float scanner=cycle/.85*1.24-.12;
  float delta=scanner-coordinate;
  float band=1-smoothstep(.014,.052,abs(delta));
  float wake=smoothstep(-.012,.012,delta)*(1-smoothstep(.03,.29,delta));
  float reveal=smoothstep(-.03,.025,delta)*(1-smoothstep(1.18,1.57,cycle));
  float edge=pow(1-saturate(abs(dot(normalize(i.n+1e-7),normalize(GetCameraPositionWS()-i.world)))),2.4);
  float3 gridPos=i.local*18;
  float3 gridLine=1-smoothstep(.035,.035+max(fwidth(gridPos),.018),abs(frac(gridPos)-.5));
  float grid=max(gridLine.x,max(gridLine.y,gridLine.z));
  float alpha=lerp(.002+edge*.012+band*.018+wake*grid*.008,.40+band*.35+wake*.10+reveal*.06,_Wire);
  alpha+=_SocketLock*lerp(.008,.14,_Wire);
  half3 colour=lerp(half3(.025,.42,.95),half3(.28,.94,1),saturate(band+wake*.40+_SocketLock*.35));
  float sceneDepth=LinearEyeDepth(SampleSceneDepth(i.p.xy/_ScaledScreenParams.xy),_ZBufferParams);
  float partDepth=-TransformWorldToView(i.world).z;
  float hidden=step(sceneDepth+.008,partDepth);
  // Occluded contours stay faint but legible; use one URP pass rather than a skipped second pass.
  alpha*=lerp(1,lerp(.025,.30,_Wire),hidden);
  return half4(colour,saturate(alpha));
 }
 half4 frag(V i):SV_Target{return Scan(i);}
 ENDHLSL
 Pass {Name "Complete structural scan" Tags {"LightMode"="SRPDefaultUnlit"} ZWrite Off ZTest Always Cull Back Blend SrcAlpha OneMinusSrcAlpha
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma target 3.0
 #pragma multi_compile_instancing
 ENDHLSL
 }
}}
